using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Atlas.Extensions;
using Atlas.Features.Backup.Contracts;
using Atlas.Features.Backup.Migrations;
using Atlas.Features.Image;

namespace Atlas.Features.Backup;

/// <summary>
///     备份清单（manifest.json）的读取与校验，导出、检查与恢复共用；版本迁移由
///     <see cref="BackupManifestMigrator" /> 完成，这里只处理当前格式
/// </summary>
internal static class BackupManifestBuilder
{
    internal const string ManifestEntryName = "manifest.json";

    internal static async Task<JsonObject> ReadManifestNodeAsync(
        ZipArchive archive, CancellationToken ct)
    {
        var entry = archive.GetEntry(ManifestEntryName)
                    ?? throw new InvalidRequestException(
                        "备份文件缺少 manifest.json");

        await using var stream = entry.Open();

        try
        {
            var node = await JsonNode.ParseAsync(
                stream, cancellationToken: ct);

            return node as JsonObject
                   ?? throw new InvalidRequestException("备份清单无法解析");
        }
        catch (JsonException)
        {
            throw new InvalidRequestException("备份清单无法解析");
        }
    }

    internal static int ReadManifestVersion(JsonObject manifest)
    {
        if (manifest["version"] is JsonValue value
            && value.TryGetValue<int>(out var version))
            return version;

        throw new InvalidRequestException("备份清单缺少版本号");
    }

    /// <summary>迁移完成后再映射为当前类型，历史结构不进反序列化代码</summary>
    internal static BackupManifest DeserializeManifest(JsonObject manifest)
    {
        try
        {
            return JsonSerializer.Deserialize(
                       manifest.ToJsonString(),
                       BackupJsonContext.Default.BackupManifest)
                   ?? throw new InvalidRequestException("备份清单无法解析");
        }
        catch (JsonException)
        {
            throw new InvalidRequestException("备份清单无法解析");
        }
    }

    /// <summary>读取并迁移到当前格式后校验，恢复与检查共用</summary>
    internal static async Task<BackupManifest> LoadManifestAsync(
        ZipArchive archive, BackupManifestMigrator migrator, CancellationToken ct)
    {
        var manifestNode = await ReadManifestNodeAsync(archive, ct);
        // 先迁移到当前格式再校验，版本不兼容时不动现有数据
        migrator.Migrate(manifestNode, ReadManifestVersion(manifestNode));
        var manifest = DeserializeManifest(manifestNode);
        ValidateManifest(manifest);
        return manifest;
    }

    internal static void ValidateManifest(BackupManifest manifest)
    {
        var sections = BackupSectionNames.FromNames(manifest.Sections);

        // 未选中的段必须留 null；空数组会被按“恢复成空”处理，两者不能混用
        RequireSection(manifest.Settings is not null, sections, BackupSection.Settings, "设置");
        RequireSection(manifest.Cards is not null, sections, BackupSection.Cards, "卡片");
        RequireSection(manifest.Images is not null, sections, BackupSection.Images, "图库");
        RequireSection(manifest.Live2D is not null, sections, BackupSection.Live2D, "Live2D");

        if (sections.HasFlag(BackupSection.Live2D) && manifest.Live2D!.Configs is null)
            throw new InvalidRequestException("备份清单缺少 Live2D 配置");

        if (sections.HasFlag(BackupSection.Cards)) ValidateCards(manifest.Cards!);
        if (sections.HasFlag(BackupSection.Images)) ValidateImages(manifest.Images!);
    }

    private static void ValidateCards(List<BackupCard> cards)
    {
        var uuids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var card in cards)
        {
            if (card is null || string.IsNullOrWhiteSpace(card.Uuid))
                throw new InvalidRequestException("备份中存在无效卡片");

            if (!uuids.Add(card.Uuid))
                throw new InvalidRequestException(
                    $"备份中存在重复卡片：{card.Uuid}");
        }
    }

    private static void ValidateImages(List<BackupImageInfo> images)
    {
        var imagePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var image in images)
        {
            if (image is null
                || string.IsNullOrWhiteSpace(image.RelativePath)
                || !BackupUploadPaths.TryResolveUploadEntry(
                    image.RelativePath, out var folder, out _))
                throw new InvalidRequestException("备份中存在无效图片路径");

            var expectedFolder = image.Kind == ImageService.KindIcon
                ? "icons"
                : "images";
            if (folder != expectedFolder)
                throw new InvalidRequestException(
                    $"图片类型与路径不匹配：{image.RelativePath}");

            if (!imagePaths.Add(ZipArchiveSafety.NormalizeRelativePath(
                    image.RelativePath)))
                throw new InvalidRequestException(
                    $"备份中存在重复图片：{image.RelativePath}");
        }
    }

    private static void RequireSection(
        bool present, BackupSection sections, BackupSection section, string name)
    {
        if (present == sections.HasFlag(section)) return;

        throw new InvalidRequestException(
            sections.HasFlag(section)
                ? $"备份清单缺少{name}内容"
                : $"备份清单包含{name}内容但未声明");
    }
}