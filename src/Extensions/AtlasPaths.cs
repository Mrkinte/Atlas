namespace Atlas.Extensions;

public enum DataDirectorySource
{
    CommandLine,
    Environment,
    Configuration,
    Default
}

/// <summary>
///     可写数据根目录的统一解析。数据库、证书与上传文件都需要跨升级保留，
///     统一放在同一个根目录下；未配置时退回 ContentRoot
/// </summary>
public sealed class AtlasPaths
{
    private const string ConfigKey = "Atlas:DataDirectory";

    /// <summary>
    ///     .NET 只把双下划线映射为层级分隔符，ATLAS_DATA_DIR 不会自动落到
    ///     Atlas:DataDirectory 上，因此显式兼容单下划线写法，与安装包脚本习惯一致
    /// </summary>
    private const string EnvironmentKey = "ATLAS_DATA_DIR";

    private AtlasPaths(string dataDirectory, DataDirectorySource source)
    {
        DataDirectory = dataDirectory;
        Source = source;
    }

    public string DataDirectory { get; }

    public DataDirectorySource Source { get; }

    public string LogsDirectory => Path.Combine(DataDirectory, "logs");

    public string CertsDirectory => Path.Combine(DataDirectory, "certs");

    public string UploadsDirectory => Path.Combine(DataDirectory, "uploads");

    /// <summary>
    ///     优先级：命令行 --data-dir > ATLAS_DATA_DIR > Atlas:DataDirectory > ContentRoot。
    ///     相对路径基于 ContentRoot 绝对化，避免换个工作目录启动就变成另一份数据
    /// </summary>
    public static AtlasPaths Resolve(
        IConfiguration configuration,
        string contentRootPath,
        string? commandLineDataDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(commandLineDataDirectory))
            return new AtlasPaths(
                ToAbsolute(commandLineDataDirectory, contentRootPath),
                DataDirectorySource.CommandLine);

        var environment = configuration[EnvironmentKey];

        if (!string.IsNullOrWhiteSpace(environment))
            return new AtlasPaths(
                ToAbsolute(environment, contentRootPath),
                DataDirectorySource.Environment);

        var configured = configuration[ConfigKey];

        if (!string.IsNullOrWhiteSpace(configured))
            return new AtlasPaths(
                ToAbsolute(configured, contentRootPath),
                DataDirectorySource.Configuration);

        return new AtlasPaths(contentRootPath, DataDirectorySource.Default);
    }

    private static string ToAbsolute(string path, string contentRootPath)
    {
        return Path.GetFullPath(path.Trim(), contentRootPath);
    }

    /// <summary>
    ///     启动时建好数据目录并探测写权限，权限不足立即失败并指出具体路径，
    ///     避免拖到首次上传或写库时才报错
    /// </summary>
    public void EnsureWritable(ILogger logger)
    {
        foreach (var directory in new[] { DataDirectory, CertsDirectory, UploadsDirectory })
            Directory.CreateDirectory(directory);

        var probeFile = Path.Combine(DataDirectory, $".write-probe-{Guid.NewGuid():N}");

        try
        {
            File.WriteAllText(probeFile, string.Empty);
            File.Delete(probeFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"数据目录不可写：{DataDirectory}。请检查目录权限，或用 --data-dir 指定其它目录（也可通过 {ConfigKey} 或环境变量 ATLAS_DATA_DIR 配置）。",
                ex);
        }

        logger.LogInformation("数据目录：{DataDirectory}", DataDirectory);
    }
}