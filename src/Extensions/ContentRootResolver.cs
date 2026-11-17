namespace Atlas.Extensions;

internal static class ContentRootResolver
{
    private const string EnvironmentKey = "ASPNETCORE_CONTENTROOT";

    public static string Resolve()
    {
        var configured = Environment.GetEnvironmentVariable(EnvironmentKey);

        if (!string.IsNullOrWhiteSpace(configured))
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));

        var current = Directory.GetCurrentDirectory();

        // 开发时的项目目录和安装后的程序目录都有 appsettings.json，优先保持当前目录
        return File.Exists(Path.Combine(current, "appsettings.json"))
            ? current
            : Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
    }
}
