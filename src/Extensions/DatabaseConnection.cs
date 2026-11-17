namespace Atlas.Extensions;

/// <summary>
///     SQLite 连接串的统一解析。相对路径一律相对数据根目录绝对化，避免换个工作目录
///     就变成另一个空库；FreeSql 与 /api/about 的大小统计共用这里
/// </summary>
internal static class DatabaseConnection
{
    private const string DefaultFileName = "atlas.db";

    private static readonly string[] PathKeys = ["Data Source", "DataSource", "Filename"];

    public static string Resolve(string? connectionString, string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return $"Data Source={Path.Combine(dataDirectory, DefaultFileName)}";

        return TryGetFilePath(connectionString, out var filePath)
            ? ReplacePath(connectionString, ToAbsolute(filePath, dataDirectory))
            : connectionString;
    }

    /// <summary>内存库或无法解析时返回 null</summary>
    public static string? GetFilePath(string? connectionString, string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return Path.Combine(dataDirectory, DefaultFileName);

        if (!TryGetFilePath(connectionString, out var filePath)) return null;

        var path = ToAbsolute(filePath, dataDirectory);
        return path == ":memory:" ? null : path;
    }

    private static string ToAbsolute(string path, string dataDirectory)
    {
        if (path == ":memory:" || Path.IsPathRooted(path)) return path;
        return Path.GetFullPath(Path.Combine(dataDirectory, path));
    }

    private static bool TryGetFilePath(string connectionString, out string filePath)
    {
        filePath = string.Empty;

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2) continue;

            if (!IsPathKey(pair[0])) continue;

            var value = pair[1].Trim().Trim('"', '\'');
            if (value.Length == 0) continue;

            filePath = value;
            return true;
        }

        return false;
    }

    private static string ReplacePath(string connectionString, string absolutePath)
    {
        var parts = connectionString.Split(';');

        for (var i = 0; i < parts.Length; i++)
        {
            var pair = parts[i].Split('=', 2);
            if (pair.Length != 2 || !IsPathKey(pair[0])) continue;

            parts[i] = $"{pair[0].Trim()}={absolutePath}";
        }

        return string.Join(';', parts);
    }

    private static bool IsPathKey(string key)
    {
        var trimmed = key.Trim();
        return PathKeys.Any(name =>
            trimmed.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}