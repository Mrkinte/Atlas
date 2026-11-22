using System.Security.Claims;
using System.Text;
using Atlas.Features.About;
using Atlas.Features.Auth;
using Atlas.Features.Auth.Entities;
using Atlas.Features.Backup;
using Atlas.Features.Backup.Migrations;
using Atlas.Features.Card;
using Atlas.Features.Database;
using Atlas.Features.Demo;
using Atlas.Features.Docker;
using Atlas.Features.Https;
using Atlas.Features.Image;
using Atlas.Features.IpInfo;
using Atlas.Features.Live2D;
using Atlas.Features.Settings;
using Atlas.Features.SystemStatus;
using Atlas.Features.Update;
using FreeSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
#if DEBUG
using Microsoft.OpenApi;
#endif

namespace Atlas.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        AtlasPaths paths)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtKeyProvider>((options, keyProvider) =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(keyProvider.GetKey())),
                    ValidateIssuer = true,
                    ValidIssuer = JwtDefaults.Issuer,
                    ValidateAudience = true,
                    ValidAudience = JwtDefaults.Audience,
                    ClockSkew = TimeSpan.Zero
                };

                // JWT 事件：先从 Cookie 取 Token，验签通过后再确认未被吊销
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrEmpty(context.Token)
                            && context.Request.Cookies.TryGetValue(
                                AuthCookie.NameFor(context.Request), out var token))
                            context.Token = token;

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        var jti = principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);

                        if (string.IsNullOrEmpty(jti))
                        {
                            context.Fail("Token 缺少 jti。");
                            return;
                        }

                        var store = context.HttpContext.RequestServices.GetRequiredService<RevokedTokenStore>();

                        if (await store.IsRevokedAsync(jti, context.HttpContext.RequestAborted))
                        {
                            context.Fail("Token 已失效。");
                            return;
                        }

                        // 密码版本不一致说明密码已重置，旧 Token 立即失效
                        var idValue = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        var versionValue = principal?.FindFirstValue(JwtDefaults.PasswordVersionClaim);

                        if (!int.TryParse(idValue, out var userId)
                            || !int.TryParse(versionValue, out var version))
                        {
                            context.Fail("Token 缺少用户标识或密码版本。");
                            return;
                        }

                        var fsql = context.HttpContext.RequestServices.GetRequiredService<IFreeSql>();

                        var currentVersion = await fsql.Select<UserEntity>()
                            .Where(u => u.Id == userId)
                            .FirstAsync(u => (int?)u.PasswordVersion,
                                context.HttpContext.RequestAborted);

                        if (currentVersion != version) context.Fail("Token 已失效。");
                    }
                };
            });

        // 兜底授权策略：未显式标注的端点一律要求登录，避免新增接口忘记加鉴权；
        // 匿名端点需显式 AllowAnonymous
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        // 反向代理后取真实客户端 IP 与协议；默认仅信任本机代理，跨机部署需另行配置 KnownProxies
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                                       | ForwardedHeaders.XForwardedProto
                                       | ForwardedHeaders.XForwardedHost;
        });

        // OpenAPI 仅开发期使用，Release 不编译进来，避免把整个 schema 库带进 AOT 镜像
#if DEBUG
        services.AddOpenApi(options =>
        {
            options.AddSchemaTransformer((schema, _, _) =>
            {
                // int32 统一输出为纯 integer
                if (schema.Format == "int32" && schema.Type.HasValue) schema.Type = JsonSchemaType.Integer;

                return Task.CompletedTask;
            });
        });
#endif

        services.AddSingleton<IFreeSql>(_ =>
        {
            // 相对路径统一相对数据目录绝对化
            var connStr = DatabaseConnection.Resolve(
                configuration.GetConnectionString("Atlas"), paths.DataDirectory);

            return new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, connStr)
                .UseAdoConnectionPool(true)
                .UseAutoSyncStructure(environment.IsDevelopment()) // 生产环境关闭自动同步
                .Build();
        });

        // HSTS 只在启用 HTTPS 时由管道调用 UseHsts 输出，纯 HTTP 部署不会下发
        services.AddHsts(options => { options.MaxAge = TimeSpan.FromDays(365); });

        services.AddSingleton<JwtKeyProvider>();
        services.AddSingleton<DatabaseMigrator>();

        services.AddSingleton<AppInitializer>();

        return services;
    }

    public static IServiceCollection AddFeatureServices(this IServiceCollection services)
    {
        services.AddSingleton<DemoMode>();
        services.AddDemoJsonContext();

        services.AddScoped<AuthTokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<RevokedTokenStore>();
        services.AddScoped<RefreshTokenStore>();
        services.AddSingleton<LoginAttemptLimiter>();
        services.AddSingleton<IpRateLimiter>();
        services.AddAuthJsonContext();

        services.AddScoped<CardService>();
        services.AddScoped<CardGroupService>();
        services.AddCardJsonContext();

        services.AddScoped<ImageService>();
        services.AddScoped<FaviconService>();
        services.AddImageJsonContext();
        services.AddHttpClient();

        services.AddScoped<SettingsService>();
        services.AddSettingsJsonContext();

        services.AddScoped<Live2DModelService>();
        services.AddScoped<Live2DConfigService>();
        services.AddLive2DJsonContext();

        services.AddScoped<BackupExportService>();
        services.AddScoped<BackupRestoreService>();
        services.AddScoped<BackupInspectService>();
        services.AddScoped<BackupService>();
        services.AddSingleton<BackupManifestMigrator>();

        services.AddBackupJsonContext();

        services.AddHttpsJsonContext();

        services.AddSingleton<SystemStatusSampler>();
        services.AddSingleton<SystemStatusConnectionLimiter>();
        services.AddSingleton<SystemStatusRuntime>();
        services.AddSystemStatusJsonContext();

        services.AddScoped<AboutService>();
        services.AddAboutJsonContext();

        services.AddSingleton<IpInfoService>();
        services.AddIpInfoJsonContext();

        services.AddSingleton<DockerStatusService>();
        services.AddDockerJsonContext();

        services.AddSingleton<UpdateService>();
        services.AddUpdateJsonContext();

        return services;
    }
}