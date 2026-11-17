using Atlas.Extensions;
using Atlas.Features.Backup.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Features.Backup;

public static class BackupEndpoints
{
    public static IEndpointRouteBuilder MapBackupEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/backup")
            .WithTags("Backup")
            .RequireAuthorization();

        group.MapGet("/export", ExportBackup);
        // IFormFile 端点需要关闭防伪校验，原因同图片上传接口
        group.MapPost("/inspect", InspectBackup).DisableAntiforgery();
        group.MapPost("/restore", RestoreBackup).DisableAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> ExportBackup(
        BackupService service, string? sections, CancellationToken ct)
    {
        var fileName = $"atlas-backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip";

        try
        {
            var stream = await service.CreateExportArchiveAsync(
                BackupSectionNames.Parse(sections), ct);
            return TypedResults.File(stream, "application/zip", fileName);
        }
        catch (InvalidRequestException e)
        {
            // 总量超过恢复上限时拒绝生成无法导回的备份
            return ProblemResults.InvalidRequest(e.Message);
        }
    }

    private static async Task<Results<Ok<BackupInspectResponse>, ProblemHttpResult>>
        InspectBackup(
            IFormFile? file, BackupService service, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return ProblemResults.InvalidRequest("请选择备份文件");

        try
        {
            return TypedResults.Ok(await service.InspectAsync(file, ct));
        }
        catch (InvalidRequestException e)
        {
            return ProblemResults.InvalidRequest(e.Message);
        }
    }

    private static async Task<Results<Ok<BackupRestoreResponse>, ProblemHttpResult>>
        RestoreBackup(
            IFormFile? file,
            BackupService service,
            string? sections,
            CancellationToken ct)
    {
        if (file is null || file.Length == 0) return ProblemResults.InvalidRequest("请选择备份文件");

        try
        {
            return TypedResults.Ok(await service.RestoreAsync(
                file, BackupSectionNames.Parse(sections), ct));
        }
        catch (InvalidRequestException e)
        {
            return ProblemResults.InvalidRequest(e.Message);
        }
    }
}