using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace Atlas.Extensions;

/// <summary>
///     静态资源与前端路由兜底。这些中间件必须在路由与授权之前注册，
///     否则会被兜底授权策略先拦截成 401
/// </summary>
public static class StaticFileExtensions
{
    // .moc3 没有默认类型映射，不补充会被静态文件中间件判为未知类型而 404
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider =
        CreateContentTypeProvider();

    private static FileExtensionContentTypeProvider CreateContentTypeProvider()
    {
        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".moc3"] = "application/octet-stream";
        return provider;
    }

    /// <summary>
    ///     上传模型目录以 GUID 命名且不可替换，可以长期缓存；内置模型随版本更新，只做短缓存，
    ///     避免升级后仍命中旧贴图。清单文件可能被手工修改，交给浏览器按请求头重新校验
    /// </summary>
    private static void ApplyLive2DCacheHeaders(StaticFileResponseContext context)
    {
        var requestPath = context.Context.Request.Path;
        var isUploaded = requestPath.StartsWithSegments("/uploads/live2d");
        var isBuiltIn = requestPath.StartsWithSegments("/live2d");
        if (!isUploaded && !isBuiltIn) return;

        if (context.File.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return;

        context.Context.Response.Headers.CacheControl = isUploaded
            ? "public,max-age=31536000,immutable"
            : "public,max-age=86400";
    }

    /// <summary>
    ///     上传目录必须显式创建并挂载：默认的 WebRootFileProvider 只覆盖 wwwroot，
    ///     而 wwwroot 启动时可能不存在。目录取自数据根目录，换 Atlas:DataDirectory 时文件会跟着走
    /// </summary>
    public static void UseUploadStaticFiles(this WebApplication app)
    {
        var uploadsDirectory = app.Services.GetRequiredService<AtlasPaths>().UploadsDirectory;
        Directory.CreateDirectory(uploadsDirectory);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(uploadsDirectory),
            RequestPath = "/uploads",
            ContentTypeProvider = ContentTypeProvider,
            OnPrepareResponse = context =>
            {
                ApplyLive2DCacheHeaders(context);

                // 抓取来的 SVG 图标不可信，直接访问时禁止执行脚本，避免 XSS
                if (Path.GetExtension(context.File.Name).Equals(".svg", StringComparison.OrdinalIgnoreCase))
                    context.Context.Response.Headers["Content-Security-Policy"] =
                        "default-src 'none'; style-src 'unsafe-inline'";
            }
        });
    }

    /// <summary>前端构建产物（wwwroot）的默认文档与静态文件</summary>
    public static void UseSpaStaticFiles(this WebApplication app)
    {
        var webRootFileProvider = CreateWebRootFileProvider(app);

        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = webRootFileProvider });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = webRootFileProvider,
            ContentTypeProvider = ContentTypeProvider,
            OnPrepareResponse = ApplyLive2DCacheHeaders
        });
    }

    /// <summary>
    ///     前端路由兜底：除接口、上传目录与带扩展名的资源外，GET 请求统一交给 index.html
    ///     必须放在路由与授权之前，避免未匹配的页面请求被兜底授权策略拦截成 401
    /// </summary>
    public static void UseSpaFallback(this WebApplication app)
    {
        var indexFile = CreateWebRootFileProvider(app).GetFileInfo("index.html");

        app.Use(async (context, next) =>
        {
            var path = context.Request.Path;

            if (!HttpMethods.IsGet(context.Request.Method)
                || Path.HasExtension(path.Value)
                || path.StartsWithSegments("/api")
                || path.StartsWithSegments("/uploads"))
            {
                await next(context);
                return;
            }

            if (!indexFile.Exists)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(indexFile);
        });
    }

    /// <summary>wwwroot 可能不存在，必须先创建再取提供器，否则拿到的是空目录</summary>
    private static PhysicalFileProvider CreateWebRootFileProvider(WebApplication app)
    {
        var webRootDirectory = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        Directory.CreateDirectory(webRootDirectory);
        return new PhysicalFileProvider(webRootDirectory);
    }
}