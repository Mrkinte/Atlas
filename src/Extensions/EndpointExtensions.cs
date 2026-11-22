using Atlas.Features.About;
using Atlas.Features.Auth;
using Atlas.Features.Backup;
using Atlas.Features.Card;
using Atlas.Features.Demo;
using Atlas.Features.Docker;
using Atlas.Features.Https;
using Atlas.Features.Image;
using Atlas.Features.IpInfo;
using Atlas.Features.Live2D;
using Atlas.Features.Settings;
using Atlas.Features.Settings.Entities;
using Atlas.Features.SystemStatus;
using Atlas.Features.Update;

namespace Atlas.Extensions;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapFeatureEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapAuthEndpoints();
        app.MapDemoEndpoints();
        app.MapCardEndpoints();
        app.MapCardGroupEndpoints();
        app.MapImageEndpoints();
        app.MapLive2DEndpoints();
        app.MapSettingsEndpoints();
        app.MapHttpsEndpoints();
        app.MapBackupEndpoints();
        app.MapSystemStatusEndpoints();
        app.MapAboutEndpoints();
        app.MapUpdateEndpoints();
        app.MapIpInfoEndpoints();
        app.MapDockerEndpoints();
        return app;
    }

    /// <summary>
    ///     健康检查，供反代、容器与监控探活使用，数据库可读即视为健康
    ///     放在 /api 下，避免被前端路由兜底成 index.html
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoint(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", Health).AllowAnonymous().WithTags("Health");
        return app;
    }

    // 这里不往 Service 层下沉：基础设施探活，不是业务查询
    private static async Task<IResult> Health(IFreeSql fsql, CancellationToken ct)
    {
        try
        {
            await fsql.Select<SettingsEntity>().CountAsync(ct);
            return Results.Text("ok", "text/plain");
        }
        catch (Exception)
        {
            return Results.Text(
                "unavailable", "text/plain", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>
    ///     未匹配路径的 404 兜底。未标注授权的接口会命中兜底策略返回 401，
    ///     这里显式返回 404 并放开匿名；上传目录缺失的文件同理
    /// </summary>
    public static IEndpointRouteBuilder MapNotFoundFallbacks(
        this IEndpointRouteBuilder app)
    {
        app.Map("/api/{**path}", NotFound).AllowAnonymous();
        app.Map("/uploads/{**path}", NotFound).AllowAnonymous();
        return app;
    }

    private static Task NotFound(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }
}