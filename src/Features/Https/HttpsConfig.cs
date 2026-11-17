using System.Text.Json;

namespace Atlas.Features.Https;

public sealed record HttpsConfig(
    bool Enabled,
    int Port);

/// <summary>
///     HTTPS 监听配置的读写。监听地址只能在进程启动时确定，读取不能依赖数据库等
///     启动后才可用的服务，因此独立保存在 certs/https.json，与证书同目录便于整体迁移
/// </summary>
public sealed class HttpsConfigStore
{
    public const int DefaultPort = 5443;

    public const int PortMin = 1;

    public const int PortMax = 65535;

    private readonly string _filePath;

    public HttpsConfigStore(string certsDirectory)
    {
        DirectoryPath = certsDirectory;
        _filePath = Path.Combine(DirectoryPath, "https.json");
    }

    public string DirectoryPath { get; }

    public static HttpsConfig Default => new(false, DefaultPort);

    public static bool IsValidPort(int port)
    {
        return port is >= PortMin and <= PortMax;
    }

    /// <summary>文件缺失或损坏时退回默认值，不影响启动</summary>
    public HttpsConfig Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return Default;

            var config = JsonSerializer.Deserialize(
                File.ReadAllBytes(_filePath),
                HttpsJsonContext.Default.HttpsConfig);

            return config is null ? Default : Normalize(config);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Default;
        }
    }

    /// <summary>先写临时文件再替换，避免中断后留下半截文件</summary>
    public HttpsConfig Save(bool enabled, int port)
    {
        var config = Normalize(new HttpsConfig(enabled, port));

        Directory.CreateDirectory(DirectoryPath);
        var tempPath = _filePath + ".tmp";

        File.WriteAllBytes(
            tempPath,
            JsonSerializer.SerializeToUtf8Bytes(config, HttpsJsonContext.Default.HttpsConfig));
        File.Move(tempPath, _filePath, true);

        return config;
    }

    private static HttpsConfig Normalize(HttpsConfig config)
    {
        return config with { Port = IsValidPort(config.Port) ? config.Port : DefaultPort };
    }
}

/// <summary>启动时生效的 HTTPS 状态，用于判断设置改动是否需要重启</summary>
public sealed record HttpsRuntimeState(
    bool StartedEnabled,
    int StartedPort)
{
    public bool IsRestartRequired(HttpsConfig config)
    {
        return config.Enabled != StartedEnabled || config.Port != StartedPort;
    }
}