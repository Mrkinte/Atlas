namespace Atlas.Features.Demo;

public static class DemoDefaults
{
    public const string Username = "demo";

    public const string SessionPath = "/api/demo/session";
}

public sealed class DemoMode
{
    private const string ConfigKey = "Atlas:Demo";

    // 与 ATLAS_DATA_DIR 一致：单下划线环境变量不会自动映射到分层配置
    private const string EnvironmentKey = "ATLAS_DEMO";

    public DemoMode(IConfiguration configuration)
    {
        // 环境变量优先：appsettings 里的默认 false 会把 ATLAS_DEMO=1 盖掉
        var value = configuration[EnvironmentKey];

        if (string.IsNullOrWhiteSpace(value)) value = configuration[ConfigKey];

        // 镜像与脚本里习惯写 ATLAS_DEMO=1，bool.TryParse 不认识
        Enabled = bool.TryParse(value, out var enabled) ? enabled : value == "1";
    }

    public bool Enabled { get; }
}