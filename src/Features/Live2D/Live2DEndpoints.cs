using Atlas.Extensions;
using Atlas.Features.Live2D.Contracts;
using Atlas.Features.Settings;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Features.Live2D;

public static class Live2DEndpoints
{
    public static IEndpointRouteBuilder MapLive2DEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/live2d/models")
            .WithTags("Live2D")
            .RequireAuthorization();

        group.MapGet("/", GetModels);
        // 与图片上传一致：没有启用 UseAntiforgery，需要显式关闭绑定 IFormFile 带来的防伪校验
        group.MapPost("/", UploadModel).DisableAntiforgery();
        group.MapDelete("/{id}", DeleteModel);

        var configGroup = endpoints.MapGroup("/api/live2d/config")
            .WithTags("Live2D");

        // 首页未登录也要能加载看板娘，读取配置保持匿名；写入必须登录
        configGroup.MapGet("/", GetConfig).AllowAnonymous();
        configGroup.MapPut("/", UpdateConfig).RequireAuthorization();

        return endpoints;
    }

    private static Ok<List<Live2DModelResponse>> GetModels(
        Live2DModelService service)
    {
        return TypedResults.Ok(service.GetModels());
    }

    private static async Task<Results<Created<Live2DModelResponse>, ProblemHttpResult>>
        UploadModel(
            IFormFile? file,
            [FromQuery] string? name,
            Live2DModelService service,
            CancellationToken ct)
    {
        var error = ValidateArchive(file);
        if (error is not null) return ProblemResults.InvalidRequest(error);

        try
        {
            var model = await service.SaveAsync(file!, name, ct);
            return TypedResults.Created("/api/live2d/models", model);
        }
        catch (InvalidRequestException e)
        {
            return ProblemResults.InvalidRequest(e.Message);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteModel(
        string id,
        Live2DModelService service,
        Live2DConfigService configService,
        SettingsService settingsService,
        CancellationToken ct)
    {
        if (!service.Delete(id)) return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);

        var prefix = $"/uploads/live2d/{id}/";

        // 模型文件删除后配置一并清理，避免遗留孤儿记录
        configService.DeleteByModelPrefix(prefix);

        // 删除的是当前使用的模型时回退内置模型，避免配置指向不存在的文件
        await settingsService.ResetLive2DModelPathIfUnderAsync(prefix, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<Live2DModelConfigResponse>, ProblemHttpResult>>
        GetConfig(
            [FromQuery] string? modelPath,
            Live2DConfigService service,
            CancellationToken ct)
    {
        try
        {
            return TypedResults.Ok(await service.GetAsync(modelPath, ct));
        }
        catch (InvalidRequestException e)
        {
            return ProblemResults.InvalidRequest(e.Message);
        }
    }

    private static async Task<Results<Ok<Live2DModelConfigResponse>, ProblemHttpResult>>
        UpdateConfig(
            UpdateLive2DModelConfigRequest request,
            Live2DConfigService service,
            CancellationToken ct)
    {
        try
        {
            return TypedResults.Ok(await service.SaveAsync(request, ct));
        }
        catch (InvalidRequestException e)
        {
            return ProblemResults.InvalidRequest(e.Message);
        }
    }

    /// <summary>校验压缩包大小与扩展名，通过时返回 null</summary>
    private static string? ValidateArchive(IFormFile? file)
    {
        if (file is null || file.Length == 0 || file.Length > Live2DLimits.MaxArchiveSize)
            return "请上传不超过 100 MB 的 ZIP 压缩包";

        return Path.GetExtension(file.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase)
            ? null
            : "只支持 ZIP 压缩包";
    }
}