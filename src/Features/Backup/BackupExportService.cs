using System.IO.Compression;
using System.Text.Json;
using Atlas.Extensions;
using Atlas.Features.Backup.Contracts;
using Atlas.Features.Card;
using Atlas.Features.Card.Entities;
using Atlas.Features.Image.Entities;
using Atlas.Features.Live2D;
using Atlas.Features.Live2D.Entities;
using Atlas.Features.Settings;
using Atlas.Features.Settings.Contracts;

namespace Atlas.Features.Backup;

/// <summary>
///     备份导出：从数据库构建清单，把上传目录中的图片、图标与 Live2D 模型
///     按安全规则收集后打包为 ZIP。清单读取与校验见 <see cref="BackupManifestBuilder" />
/// </summary>
public class BackupExportService(
    IFreeSql fsql,
    SettingsService settingsService,
    AtlasPaths paths)
{
    /// <summary>
    ///     生成备份 ZIP 并返回可异步读取的临时文件流，响应结束后自动删除
    ///     ZipArchive 释放时会同步写入中央目录，不能直接写到 Kestrel 响应流
    /// </summary>
    public async Task<FileStream> CreateExportArchiveAsync(
        BackupSection sections = BackupSection.All,
        CancellationToken ct = default)
    {
        var manifest = await BuildManifestAsync(sections, ct);
        var tempPath = Path.Combine(
            Path.GetTempPath(), $"atlas-backup-{Guid.NewGuid():N}.zip");

        try
        {
            await using (var output = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous))
            {
                await using var archive = new ZipArchive(
                    output, ZipArchiveMode.Create, true);

                var manifestEntry = archive.CreateEntry(
                    BackupManifestBuilder.ManifestEntryName,
                    CompressionLevel.Optimal);
                await using (var entryStream = manifestEntry.Open())
                {
                    await JsonSerializer.SerializeAsync(
                        entryStream,
                        manifest,
                        BackupJsonContext.Default.BackupManifest,
                        ct);
                }

                var totalBytes = 0L;
                foreach (var file in EnumerateUploadFiles(sections))
                {
                    ct.ThrowIfCancellationRequested();

                    totalBytes += new FileInfo(file.FullPath).Length;
                    if (totalBytes > BackupLimits.MaxTotalUncompressedBytes)
                        // 超过恢复上限的包无法再导回，直接拒绝生成
                        throw new InvalidRequestException(
                            "上传文件总大小超过 1 GB，无法生成备份");

                    var entry = archive.CreateEntry(
                        file.RelativePath, CompressionLevel.Fastest);
                    await using var source = File.OpenRead(file.FullPath);
                    await using var target = entry.Open();
                    await source.CopyToAsync(target, ct);
                }
            }

            return new FileStream(
                tempPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        }
        catch
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }
    }

    private async Task<BackupManifest> BuildManifestAsync(
        BackupSection sections, CancellationToken ct)
    {
        var manifest = new BackupManifest
        {
            Version = BackupLimits.CurrentVersion,
            AppVersion = ResolveAppVersion(),
            ExportedAt = DateTime.UtcNow,
            Sections = [.. BackupSectionNames.ToNames(sections)]
        };

        SettingsResponse? settings = null;
        if (sections.HasFlag(BackupSection.Settings)
            || sections.HasFlag(BackupSection.Live2D))
            settings = await settingsService.GetAsync(ct);

        if (sections.HasFlag(BackupSection.Settings))
            manifest.Settings = BackupSettings.FromResponse(settings!);

        if (sections.HasFlag(BackupSection.Cards))
        {
            var cards = (await fsql.Select<CardEntity>()
                    .OrderBy(c => c.Id)
                    .ToListAsync(ct))
                .ToResponseList();
            manifest.Cards = cards.Select(BackupCard.FromResponse).ToList();

            manifest.CardGroups = (await fsql.Select<CardGroupEntity>()
                    .OrderBy(g => new { g.SortOrder, g.Id })
                    .ToListAsync(ct))
                .Select(BackupCardGroup.FromEntity)
                .ToList();
        }

        if (sections.HasFlag(BackupSection.Images))
        {
            var images = await fsql.Select<ImageEntity>()
                .OrderBy(i => i.Id)
                .ToListAsync(ct);

            // 数据库记录可能指向已丢失的文件，这类记录不纳入备份，避免恢复时必然失败
            manifest.Images = images
                .Select(image => new
                {
                    Image = image,
                    FullPath = ResolveImagePath(image.RelativePath)
                })
                .Where(it => File.Exists(it.FullPath))
                .Select(it => new BackupImageInfo(
                    ZipArchiveSafety.NormalizeRelativePath(it.Image.RelativePath),
                    it.Image.OriginalName,
                    it.Image.Kind,
                    it.Image.CreatedAt))
                .ToList();
        }

        if (sections.HasFlag(BackupSection.Live2D))
        {
            var configs = (await fsql.Select<Live2DModelConfigEntity>()
                    .OrderBy(c => c.ModelPath)
                    .ToListAsync(ct))
                .Select(config => config.ToResponse());
            manifest.Live2D = BackupLive2D.FromResponse(settings!, configs);
        }

        return manifest;
    }

    /// <summary>仅作排障信息写入清单，不参与兼容性判断</summary>
    private static string ResolveAppVersion()
    {
        return typeof(BackupExportService).Assembly.GetName().Version?.ToString()
               ?? "unknown";
    }

    private IEnumerable<(string RelativePath, string FullPath)> EnumerateUploadFiles(
        BackupSection sections)
    {
        var uploadsDirectory = paths.UploadsDirectory;
        if (!Directory.Exists(uploadsDirectory)) yield break;

        if (sections.HasFlag(BackupSection.Images))
            foreach (var folder in BackupUploadPaths.UploadFolders)
            {
                var directory = Path.Combine(uploadsDirectory, folder);
                if (!Directory.Exists(directory)) continue;

                foreach (var fullPath in Directory.EnumerateFiles(directory).Order())
                {
                    var fileName = Path.GetFileName(fullPath);
                    if (!ZipArchiveSafety.IsSafeFileName(fileName)
                        || !BackupUploadPaths.AllowedExtensions.Contains(
                            Path.GetExtension(fileName)))
                        continue;

                    yield return (
                        $"{BackupUploadPaths.UploadsPrefix}{folder}/{fileName}",
                        fullPath);
                }
            }

        if (!sections.HasFlag(BackupSection.Live2D)) yield break;

        foreach (var file in EnumerateLive2DFiles(uploadsDirectory)) yield return file;
    }

    /// <summary>递归遍历模型目录，保持 uploads/live2d/&lt;模型目录&gt;/... 结构；不安全的路径直接跳过</summary>
    private static IEnumerable<(string RelativePath, string FullPath)>
        EnumerateLive2DFiles(string uploadsDirectory)
    {
        var directory = Path.Combine(
            uploadsDirectory, BackupUploadPaths.Live2DFolder);
        if (!Directory.Exists(directory)) yield break;

        var files = Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Order();

        foreach (var fullPath in files)
        {
            var relative = ZipArchiveSafety.NormalizeRelativePath(
                Path.GetRelativePath(uploadsDirectory, fullPath));
            if (!BackupUploadPaths.TryResolveLive2DEntry(
                    $"{BackupUploadPaths.UploadsPrefix}{relative}",
                    out var entryPath))
                continue;

            yield return (entryPath, fullPath);
        }
    }

    private string ResolveImagePath(string relativePath)
    {
        if (!BackupUploadPaths.TryResolveUploadEntry(
                relativePath, out var folder, out var fileName))
            return string.Empty;

        return Path.Combine(
            paths.UploadsDirectory, folder, fileName);
    }
}