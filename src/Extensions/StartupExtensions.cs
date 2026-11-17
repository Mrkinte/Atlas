using System.Diagnostics;

namespace Atlas.Extensions;

public static class StartupExtensions
{
    public static void InitializeAtlas(this WebApplication app)
    {
        app.Services.GetRequiredService<AppInitializer>().Initialize();
    }

    /// <summary>
    ///     基础安全响应头，只做浏览器侧防护，不改变响应体
    ///     HSTS 由 UseHsts 在启用 HTTPS 时单独追加
    /// </summary>
    public static void UseSecurityHeaders(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            // 上传目录按扩展名返回文件，禁止浏览器嗅探成 HTML
            headers["X-Content-Type-Options"] = "nosniff";
            // 禁止第三方页面内嵌，避免点击劫持
            headers["X-Frame-Options"] = "SAMEORIGIN";
            // 跨站跳转不泄露完整地址（可能包含内网主机名/端口）
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            await next(context);
        });
    }

    /// <summary>记录 /api 请求的方法、路径、状态码与耗时，便于排查 4xx/5xx 与慢接口</summary>
    public static void UseApiRequestLogging(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path;

            // 健康检查通常会被探活工具高频调用，不记录以免刷屏
            if (!path.StartsWithSegments("/api") || path.StartsWithSegments("/api/health"))
            {
                await next(context);
                return;
            }

            var started = Stopwatch.GetTimestamp();

            try
            {
                await next(context);
            }
            finally
            {
                app.Logger.LogInformation(
                    "{Method} {Path} -> {StatusCode} ({ElapsedMs:0.0} ms)",
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.Response.StatusCode,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
        });
    }
}