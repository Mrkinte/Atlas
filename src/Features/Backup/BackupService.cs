using Atlas.Features.Backup.Contracts;

namespace Atlas.Features.Backup;

/// <summary>
///     备份导出、检查与恢复的入口门面，端点通过这里调用
/// </summary>
public class BackupService(
    BackupExportService exportService,
    BackupRestoreService restoreService,
    BackupInspectService inspectService)
{
    public Task<FileStream> CreateExportArchiveAsync(
        BackupSection sections = BackupSection.All,
        CancellationToken ct = default)
    {
        return exportService.CreateExportArchiveAsync(sections, ct);
    }

    public Task<BackupInspectResponse> InspectAsync(
        IFormFile file, CancellationToken ct = default)
    {
        return inspectService.InspectAsync(file, ct);
    }

    public Task<BackupRestoreResponse> RestoreAsync(
        IFormFile file,
        BackupSection sections = BackupSection.All,
        CancellationToken ct = default)
    {
        return restoreService.RestoreAsync(file, sections, ct);
    }
}