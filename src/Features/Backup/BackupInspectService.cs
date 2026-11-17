using System.IO.Compression;
using Atlas.Extensions;
using Atlas.Features.Backup.Contracts;
using Atlas.Features.Backup.Migrations;

namespace Atlas.Features.Backup;

/// <summary>恢复前读取备份包概览，不解包也不改动现有数据</summary>
public class BackupInspectService(BackupManifestMigrator manifestMigrator)
{
    public async Task<BackupInspectResponse> InspectAsync(
        IFormFile file, CancellationToken ct = default)
    {
        if (file.Length <= 0 || file.Length > BackupLimits.MaxRestoreArchiveBytes)
            throw new InvalidRequestException("备份文件为空或超过 512 MB");

        var tempPath = await BackupTempArchive.SaveAsync(file, "atlas-inspect", ct);

        try
        {
            await using var stream = File.OpenRead(tempPath);
            await using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var manifest = await BackupManifestBuilder.LoadManifestAsync(
                archive, manifestMigrator, ct);

            return new BackupInspectResponse(
                manifest.Version,
                manifest.AppVersion,
                manifest.ExportedAt,
                BackupSectionNames.ToNames(
                    BackupSectionNames.FromNames(manifest.Sections)),
                manifest.Cards?.Count ?? 0,
                manifest.Images?.Count ?? 0,
                manifest.Live2D?.Configs?.Count ?? 0);
        }
        catch (InvalidDataException)
        {
            throw new InvalidRequestException("备份文件不是有效的 ZIP");
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}