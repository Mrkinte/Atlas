namespace Atlas.Features.Demo;

public static class DemoModeMiddleware
{
    private const string BlockedBody =
        """{"type":"about:blank","title":"Forbidden","status":403,"code":"demo-readonly","detail":"演示模式：修改不会保存"}""";

    // 演示会话必须能建立、续期与登出，其余写入一律拒绝
    private static readonly string[] AllowedWrites =
    [
        "/api/auth/refresh",
        "/api/auth/logout",
        DemoDefaults.SessionPath
    ];

    /// <summary>必须位于 UseRouting 之后、限流之前，被拒绝的写入不占用限流额度</summary>
    public static IApplicationBuilder UseDemoReadOnly(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (!context.RequestServices.GetRequiredService<DemoMode>().Enabled
                || !IsBlocked(context.Request))
            {
                await next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json; charset=utf-8";
            await context.Response.WriteAsync(BlockedBody, context.RequestAborted);
        });
    }

    private static bool IsBlocked(HttpRequest request)
    {
        var path = request.Path;

        if (!path.StartsWithSegments("/api")) return false;

        // 导出会带出全部用户数据
        if (path.StartsWithSegments("/api/backup/export")) return true;

        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) return false;

        return !AllowedWrites.Any(allowed =>
            path.StartsWithSegments(allowed, StringComparison.OrdinalIgnoreCase));
    }
}