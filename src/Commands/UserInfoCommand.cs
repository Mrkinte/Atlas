using Atlas.Extensions;
using Atlas.Features.Auth;
using Atlas.Features.Auth.Entities;
using Atlas.Features.Demo;

namespace Atlas.Commands;

internal static class UserInfoCommand
{
    public static async Task<int> RunAsync(
        AuthService authService,
        DemoMode demoMode,
        AtlasPaths paths,
        string? databaseFile,
        TextWriter output,
        TextWriter error,
        CancellationToken ct)
    {
        if (databaseFile is not null && !File.Exists(databaseFile))
        {
            error.WriteLine($"错误: 数据库不存在: {databaseFile}");
            error.WriteLine("请先启动一次 Web 服务完成初始化，或用 --data-dir 指定正确的数据目录。");
            return 1;
        }

        List<UserEntity> users;

        try
        {
            users = await authService.ListAsync(ct);
        }
        catch (Exception ex)
        {
            error.WriteLine($"错误: 数据库尚未初始化或无法读取: {ex.Message}");
            error.WriteLine($"数据库: {databaseFile ?? "（内存数据库）"}");
            return 1;
        }

        output.WriteLine($"Atlas {AppVersion.Current}");
        output.WriteLine($"数据目录: {paths.DataDirectory}（来源: {CliHelp.DescribeDataSource(paths.Source)}）");
        output.WriteLine($"数据库:   {databaseFile ?? "（内存数据库）"}{FileSizeText(databaseFile)}");
        output.WriteLine($"演示模式: {(demoMode.Enabled ? "开启" : "关闭")}");
        output.WriteLine();

        if (users.Count == 0)
        {
            output.WriteLine("数据库中还没有任何账号。首次启动 Web 服务会自动创建管理员账号 atlas。");
            return 0;
        }

        output.WriteLine("ID\t用户名\t密码版本");

        foreach (var user in users)
            output.WriteLine($"{user.Id}\t{user.Username}\t{user.PasswordVersion}");

        return 0;
    }

    private static string FileSizeText(string? path)
    {
        if (path is null) return string.Empty;

        try
        {
            return $"（{FormatSize(new FileInfo(path).Length)}）";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.0} KB";
        return $"{bytes / (1024.0 * 1024.0):0.0} MB";
    }
}
