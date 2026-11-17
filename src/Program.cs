using Atlas.Commands;
using atlas.Extensions;
using Atlas.Extensions;
using Atlas.Features.Auth;
using Atlas.Features.Backup;
using Atlas.Features.Demo;
using Atlas.Features.Https;
using Atlas.Features.Live2D;
using Atlas.Features.SystemStatus;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Console;
using SQLitePCL;

CliInvocation cli;

try
{
    cli = CliInvocation.Parse(args);
}
catch (CliUsageException ex)
{
    Console.Error.WriteLine($"错误: {ex.Message}");
    Console.Error.WriteLine("使用 atlas --help 查看用法。");
    return 2;
}

// 帮助与版本不初始化 SQLite、不建宿主，保证命令零副作用
if (cli.Command == CliCommand.Help)
{
    if (cli.HelpTopic != CliCommand.None) CliHelp.WriteCommand(cli.HelpTopic, Console.Out);
    else CliHelp.WriteGeneral(Console.Out);

    return 0;
}

if (cli.Command == CliCommand.Version)
{
    CliHelp.WriteVersion(Console.Out);
    return 0;
}

var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
{
    Args = cli.ServerArgs,
    // 从任意工作目录执行时仍能找到 appsettings.json 与默认数据目录
    ContentRootPath = ContentRootResolver.Resolve()
});

// Microsoft.Data.Sqlite 依赖 SQLitePCLRaw 提供原生 e_sqlite3，必须在任何数据库访问前完成一次初始化
Batteries_V2.Init();

builder.Logging.AddSimpleConsole(options =>
{
    // 默认格式不带时间，上线后无法定位问题发生时刻
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

if (cli.HasCommand)
{
    // 命令模式的 stdout 只输出结果，日志统一走 stderr 且只保留警告以上
    builder.Services.Configure<ConsoleLoggerOptions>(
        options => options.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Logging.SetMinimumLevel(LogLevel.Warning);
}

// CreateSlimBuilder 默认不包含 HTTPS 支持，显式开启
builder.WebHost.UseKestrelHttpsConfiguration();

// 备份恢复允许上传大 ZIP，上限统一取 BackupLimits；其他接口保持 Kestrel 默认值
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = BackupLimits.MaxRestoreArchiveBytes);

// 用户数据统一挂在数据根目录下，优先级见 AtlasPaths.Resolve
var atlasPaths = AtlasPaths.Resolve(
    builder.Configuration, builder.Environment.ContentRootPath, cli.DataDirectory);
builder.Services.AddSingleton(atlasPaths);

// 文件日志：按天切割，保留 7 天；与数据库、证书、上传文件一起放在可写数据目录下
// 命令模式不写日志文件，避免只读命令创建目录
if (!cli.HasCommand) builder.Logging.AddProvider(new RollingFileLoggerProvider(atlasPaths.LogsDirectory));

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment, atlasPaths);

builder.Services.AddFeatureServices();

builder.Services.AddProblemDetails();

// HTTPS 的配置与证书要在 Build 之前准备好：监听地址只能在启动时确定
if (!cli.HasCommand) builder.AddHttps(atlasPaths);

var app = builder.Build();

// 命令行子命令：执行完直接退出，不启动 Web 服务，也不播种管理员账号
if (cli.HasCommand) return await CliRunner.RunAsync(cli, app.Services);

// 建立数据目录并探测写权限：权限不足时启动即失败，避免首次上传或写库才暴露问题
atlasPaths.EnsureWritable(app.Logger);

// 启用 HTTPS 时备好证书；此时还没有证书会先生成一张自签名证书兜底
app.PrepareHttpsCertificate();

// 反向代理后还原真实客户端 IP 与协议：限流依赖真实 IP，Cookie 的 Secure 判断依赖真实协议
app.UseForwardedHeaders();
app.UseExceptionHandler();

// 安全响应头放在异常处理之后：异常重跑管道时重新设置，错误响应也带防护头
app.UseSecurityHeaders();
app.UseApiRequestLogging();

// 备份恢复与看板娘模型上传的请求体可能超过 Kestrel 默认的 30 MB，仅对这两个端点放宽
app.Use(async (context, next) =>
{
    var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
    if (feature is { IsReadOnly: false })
    {
        if (context.Request.Path.StartsWithSegments("/api/backup/restore")
            || context.Request.Path.StartsWithSegments("/api/backup/inspect"))
            feature.MaxRequestBodySize = BackupLimits.MaxRestoreArchiveBytes;
        else if (HttpMethods.IsPost(context.Request.Method)
                 && context.Request.Path.StartsWithSegments("/api/live2d/models"))
            // 上限取 Live2DLimits，已为 multipart 开销留出余量
            feature.MaxRequestBodySize = Live2DLimits.ModelUploadRequestBodyBytes;
    }

    await next(context);
});

// 已启用 HTTPS 时把 HTTP 请求跳到 HTTPS，保证令牌 Cookie 始终走 TLS；
// 纯 HTTP 部署时 Cookie 不带 Secure，浏览器才会保存
if (app.Services.GetRequiredService<HttpsRuntimeState>().StartedEnabled)
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

// 启动期初始化：建表、JWT 密钥、管理员账号
app.InitializeAtlas();

// 预热采样器：CPU 占用率依赖 /proc/stat 两次采样的差值，预热后首个连接就有数值
app.Services.GetRequiredService<SystemStatusSampler>().Sample();

// OpenAPI 仅开发期使用：Release 发布不引用相关包，这里同步编译剔除
#if DEBUG
if (app.Environment.IsDevelopment()) app.MapOpenApi().AllowAnonymous();
#endif

// 未显式配置监听地址时只监听 localhost，局域网访问会失败，启动日志里提示
if (string.IsNullOrWhiteSpace(builder.Configuration[WebHostDefaults.ServerUrlsKey]))
    app.Logger.LogInformation(
        "未配置监听地址，默认仅监听 http://localhost:5000；局域网访问请设置 ASPNETCORE_URLS 或 Urls。");

// 静态资源与前端路由兜底，必须位于路由与授权之前
app.UseUploadStaticFiles();
app.UseSpaStaticFiles();
app.UseSpaFallback();

// WebSocket 升级需要显式启用；放在路由与授权之前，认证仍会解析升级请求的 Cookie
app.UseWebSockets();

// 认证/授权显式放在静态文件与路由之后：
// 先完成端点匹配，AllowAnonymous 等端点元数据才能生效，兜底授权策略不会误拦网页与静态资源
app.UseRouting();

// 演示模式的写入拦截只按路径判断，不依赖端点元数据
app.UseDemoReadOnly();

// 限流必须位于 UseRouting 之后：策略来自端点元数据，未完成路由匹配时限流不会生效
app.UseIpRateLimiting();

app.UseAuthentication();
app.UseAuthorization();

app.MapFeatureEndpoints();
app.MapHealthEndpoint();

// 接口与上传目录未匹配时返回 404，避免被前端兜底成 index.html
app.MapNotFoundFallbacks();

app.Run();
return 0;