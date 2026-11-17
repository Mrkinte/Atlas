using Atlas.Extensions;

namespace Atlas.Features.Https;

/// <summary>
///     HTTPS 的启动装配：监听地址只能在 Build 之前确定，证书支持运行时热替换
/// </summary>
public static class HttpsSetup
{
    public static void AddHttps(this WebApplicationBuilder builder, AtlasPaths paths)
    {
        var configStore = new HttpsConfigStore(paths.CertsDirectory);
        var config = configStore.Load();
        var certificateStore = CertificateStore.Create(paths.CertsDirectory);

        builder.Services.AddSingleton(configStore);
        builder.Services.AddSingleton(certificateStore);
        builder.Services.AddSingleton(new HttpsRuntimeState(config.Enabled, config.Port));

        // 监听地址只能在启动时确定：原有 HTTP 地址 + 启用 HTTPS 时的端口
        // 统一通过 UseUrls 写入，避免与 Urls 配置各绑一次导致 address already in use
        builder.WebHost.UseUrls(ResolveUrls(builder.Configuration, config));

        // 占位证书：Kestrel 要求配置了 ServerCertificate 或选择器才允许启用 HTTPS
        // 真正下发的证书由 OnAuthenticate 按连接覆盖——那里用的是带中间证书的上下文，
        // 而且每次握手都重新读取，所以替换证书依然无需重启
        builder.WebHost.ConfigureKestrel(options => options.ConfigureHttpsDefaults(https =>
        {
            https.ServerCertificate = certificateStore.Current;
            https.OnAuthenticate = (_, ssl) =>
            {
                ssl.ServerCertificate = null;
                ssl.ServerCertificateSelectionCallback = null;
                ssl.ServerCertificateContext = certificateStore.CurrentContext;
            };
        }));
    }

    /// <summary>
    ///     Build 之后、Run 之前调用：已启用 HTTPS 时备好证书，没有可用证书就生成自签名兜底，
    ///     否则 Kestrel 无法启动 HTTPS 监听
    /// </summary>
    public static void PrepareHttpsCertificate(this WebApplication app)
    {
        var runtime = app.Services.GetRequiredService<HttpsRuntimeState>();
        if (!runtime.StartedEnabled) return;

        // 必须在 Kestrel 解析 HTTPS 选项之前备好证书
        var certificates = app.Services.GetRequiredService<CertificateStore>();
        if (!certificates.HasCertificate)
        {
            if (certificates.Error is { } error)
            {
                // 证书文件存在但不可用：保留原文件，仅用内存里的自签名证书让 HTTPS 能启动
                app.Logger.LogWarning("HTTPS 证书不可用（{Reason}），本次启动使用临时自签名证书", error);
                certificates.UseTemporarySelfSigned();
            }
            else
            {
                certificates.CreateSelfSigned();
            }
        }

        app.Logger.LogInformation("HTTPS 已启用，监听端口 {Port}", runtime.StartedPort);
    }

    /// <summary>保留配置里的 HTTP 地址，启用 HTTPS 时追加 HTTPS 端口</summary>
    private static string[] ResolveUrls(IConfiguration configuration, HttpsConfig config)
    {
        var configured = configuration[WebHostDefaults.ServerUrlsKey];

        var urls = string.IsNullOrWhiteSpace(configured)
            ? ["http://localhost:5000"]
            : configured.Split(
                ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!config.Enabled) return urls;

        return [.. urls, $"https://*:{config.Port}"];
    }
}