using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Atlas.Extensions;
using Atlas.Features.Live2D.Contracts;

namespace Atlas.Features.Live2D;

public class Live2DModelService(AtlasPaths paths)
{
    private const int MaxEntries = 2000;

    private const int MaxIdLength = 64;

    private const int MaxNameLength = 64;

    // 展示名元数据，与模型文件放在同一目录
    private const string MetadataFileName = ".atlas.json";

    private const string UploadUrlPrefix = "/uploads/live2d/";

    /// <summary>
    ///     国内资源站下载的 ZIP 常用 GBK 文件名且不设 UTF-8 标志，默认按 CP437 解码会得到
    ///     乱码目录，模型清单里的中文路径全部 404。设置了 UTF-8 标志的条目仍按 UTF-8 解码
    /// </summary>
    private static readonly Encoding LegacyEntryNameEncoding = CreateLegacyEncoding();

    /// <summary>读取模型清单时遇到非 UTF-8 内容直接失败，避免把 GBK 清单读成乱码再写回</summary>
    private static readonly Encoding StrictUtf8 =
        new UTF8Encoding(false, true);

    private string RootDirectory => Path.Combine(paths.UploadsDirectory, "live2d");

    private static Encoding CreateLegacyEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(936);
    }

    /// <summary>扫描上传目录，返回结构完整的模型；上传中的临时目录会被跳过</summary>
    public List<Live2DModelResponse> GetModels()
    {
        var root = RootDirectory;
        if (!Directory.Exists(root)) return [];

        var models = new List<Live2DModelResponse>();
        foreach (var directory in Directory.EnumerateDirectories(root)
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            var id = Path.GetFileName(directory);
            if (!IsValidId(id)) continue;

            var metadata = ReadMetadata(directory);
            var relative = ResolveModelRelativePath(directory, metadata);
            if (relative is null) continue;

            var name = metadata.Name ?? id;
            var size = metadata.Size ?? GetDirectorySize(directory);
            if (metadata.Size is null)
                // 旧模型没有记录大小，扫描后补写元数据，避免每次请求都完整遍历目录
                WriteMetadata(directory, name, relative, size);

            models.Add(new Live2DModelResponse(
                id, name, $"{UploadUrlPrefix}{id}/{relative}", size));
        }

        return models;
    }

    /// <summary>优先使用元数据里的模型路径；旧目录缺少元数据时扫描一次并校验</summary>
    private static string? ResolveModelRelativePath(
        string directory, ModelMetadata metadata)
    {
        if (metadata.Model is not null
            && ZipArchiveSafety.IsSafeRelativePath(metadata.Model)
            && File.Exists(Path.Combine(directory, metadata.Model)))
            return metadata.Model;

        var modelFile = FindModelFile(directory);
        return modelFile is null
            ? null
            : Path.GetRelativePath(directory, modelFile).Replace('\\', '/');
    }

    /// <summary>解压上传的 ZIP 并保存为独立模型目录，展示名与目录 id 相互独立</summary>
    public async Task<Live2DModelResponse> SaveAsync(
        IFormFile file, string? name, CancellationToken ct = default)
    {
        var displayName = NormalizeDisplayName(name, file.FileName);
        if (GetModels().Any(m => string.Equals(
                m.Name, displayName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidRequestException("同名模型已存在，请更换名称");

        var modelId = Guid.NewGuid().ToString("N");
        var destination = GetDirectory(modelId);

        Directory.CreateDirectory(RootDirectory);
        var tempDirectory = Path.Combine(
            RootDirectory, $".upload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            await ExtractAsync(file, tempDirectory, ct);

            var modelFile = FindModelFile(tempDirectory)
                            ?? throw new InvalidRequestException(
                                "压缩包中没有找到 .model3.json 模型文件");
            var relative = Path.GetRelativePath(tempDirectory, modelFile)
                .Replace('\\', '/');

            TryAttachExpressions(modelFile);
            var size = GetDirectorySize(tempDirectory);
            WriteMetadata(tempDirectory, displayName, relative, size);

            Directory.Move(tempDirectory, destination);

            return new Live2DModelResponse(
                modelId, displayName, $"{UploadUrlPrefix}{modelId}/{relative}", size);
        }
        catch
        {
            DeleteDirectoryQuietly(tempDirectory);
            throw;
        }
    }

    public bool Delete(string id)
    {
        if (!IsValidId(id) || !Directory.Exists(GetDirectory(id))) return false;

        Directory.Delete(GetDirectory(id), true);
        return true;
    }

    private string GetDirectory(string id)
    {
        return Path.Combine(RootDirectory, id);
    }

    /// <summary>展示名仅用于界面展示，不参与路径拼接；缺省时取 ZIP 文件名</summary>
    private static string NormalizeDisplayName(string? name, string fileName)
    {
        var candidate = string.IsNullOrWhiteSpace(name)
            ? Path.GetFileNameWithoutExtension(fileName)
            : name.Trim();

        if (candidate.Length is 0 or > MaxNameLength)
            throw new InvalidRequestException($"模型名称不能为空且不超过 {MaxNameLength} 个字符");

        if (candidate.Any(char.IsControl)) throw new InvalidRequestException("模型名称包含非法字符");

        return candidate;
    }

    /// <summary>元数据缺失或损坏时返回空记录，由调用方回退到目录扫描</summary>
    private static ModelMetadata ReadMetadata(string directory)
    {
        var metadataPath = Path.Combine(directory, MetadataFileName);
        if (!File.Exists(metadataPath)) return new ModelMetadata(null, null, null);

        try
        {
            if (JsonNode.Parse(File.ReadAllText(metadataPath)) is not JsonObject metadata)
                return new ModelMetadata(null, null, null);

            return new ModelMetadata(
                ReadString(metadata, "name"),
                ReadString(metadata, "model"),
                ReadSize(metadata));
        }
        catch (Exception e) when (
            e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new ModelMetadata(null, null, null);
        }
    }

    private static string? ReadString(JsonObject metadata, string key)
    {
        return metadata[key] is JsonValue value
               && value.TryGetValue<string>(out var text)
               && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;
    }

    private static long? ReadSize(JsonObject metadata)
    {
        return metadata["size"] is JsonValue value
               && value.TryGetValue<long>(out var size)
               && size >= 0
            ? size
            : null;
    }

    private static void WriteMetadata(
        string directory, string displayName, string modelRelativePath, long size)
    {
        var metadata = new JsonObject
        {
            ["name"] = displayName,
            ["model"] = modelRelativePath,
            ["size"] = size
        };

        try
        {
            File.WriteAllText(
                Path.Combine(directory, MetadataFileName), metadata.ToJsonString());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // 写入失败时展示名退回目录 id，大小下次扫描时重算
        }
    }

    private static bool IsValidId(string id)
    {
        return id.Length is > 0 and <= MaxIdLength
               && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    }

    /// <summary>
    ///     模型清单缺少 Expressions 声明时，把目录里的 .exp3.json 登记进去
    ///     没有声明时运行时不会创建表情管理器，表情文件等于不存在
    ///     只补齐缺失项，已有声明保持原样
    /// </summary>
    private static void TryAttachExpressions(string modelFile)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(modelFile, StrictUtf8));
        }
        catch (Exception e) when (
            e is JsonException or IOException or DecoderFallbackException)
        {
            // 清单损坏、不可读或不是 UTF-8 时保持原样，避免改写后变乱码
            return;
        }

        if (root is not JsonObject rootObject
            || rootObject["FileReferences"] is not JsonObject references
            || references.ContainsKey("Expressions"))
            return;

        var directory = Path.GetDirectoryName(modelFile)!;
        var expressions = new JsonArray();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory
                     .EnumerateFiles(directory, "*.exp3.json", SearchOption.AllDirectories)
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(file)[..^".exp3.json".Length];
            // 不同子目录可能有同名表情，重复的 Name 会让运行时索引冲突
            if (name.Length == 0 || !names.Add(name)) continue;

            // 显式声明为 JsonNode，避免命中 JsonArray.Add<T> 的 AOT 警告
            JsonNode expression = new JsonObject
            {
                ["Name"] = name,
                ["File"] = Path.GetRelativePath(directory, file).Replace('\\', '/')
            };
            expressions.Add(expression);
        }

        if (expressions.Count == 0) return;

        references["Expressions"] = expressions;

        try
        {
            File.WriteAllText(modelFile, rootObject.ToJsonString(), StrictUtf8);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // 写不进去就维持原清单，下一次扫描时会再尝试
        }
    }

    /// <summary>存在多个 .model3.json 时取层级最浅、路径排序靠前的一个</summary>
    private static string? FindModelFile(string directory)
    {
        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".model3.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file.Count(c => c is '/' or '\\'))
            .ThenBy(file => file, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static long GetDirectorySize(string directory)
    {
        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Sum(file => new FileInfo(file).Length);
    }

    /// <summary>解压到指定目录：拒绝路径穿越与超量内容，按实际写入字节数限制总量</summary>
    private static async Task ExtractAsync(
        IFormFile file, string targetDirectory, CancellationToken ct)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            await using var archive = new ZipArchive(
                stream, ZipArchiveMode.Read, false, LegacyEntryNameEncoding);

            if (archive.Entries.Count > MaxEntries) throw new InvalidRequestException("压缩包内文件数量过多");

            var total = 0L;
            var buffer = new byte[81920];
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();
                if (entry.Name.Length == 0) continue;

                var entryPath = ZipArchiveSafety.NormalizeRelativePath(entry.FullName);
                if (!ZipArchiveSafety.IsSafeRelativePath(entryPath)) throw new InvalidRequestException("压缩包路径不合法");

                var destination = Path.GetFullPath(
                    Path.Combine(targetDirectory, entryPath));
                // 大小写不敏感地拒绝重复路径，避免后写入的条目静默覆盖前一个
                if (!written.Add(destination)) throw new InvalidRequestException("压缩包中存在重复文件");

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

                await using var source = entry.Open();
                await using var target = File.Create(destination);

                while (true)
                {
                    var read = await source.ReadAsync(buffer, ct);
                    if (read == 0) break;

                    total += read;
                    if (total > Live2DLimits.MaxExtractedTotalBytes)
                        throw new InvalidRequestException("压缩包解压后超过 200 MB");

                    await target.WriteAsync(buffer.AsMemory(0, read), ct);
                }
            }
        }
        catch (InvalidDataException)
        {
            throw new InvalidRequestException("文件不是有效的 ZIP 压缩包");
        }
        catch (Exception e) when (
            e is IOException or UnauthorizedAccessException
                or NotSupportedException or ArgumentException)
        {
            // 非法文件名（保留名、冒号等）不应变成 500，统一按压缩包内容不合法处理
            throw new InvalidRequestException("压缩包路径或文件名不合法");
        }
    }

    private static void DeleteDirectoryQuietly(string directory)
    {
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // 临时目录清理失败不影响主流程
        }
    }

    private sealed record ModelMetadata(string? Name, string? Model, long? Size);
}