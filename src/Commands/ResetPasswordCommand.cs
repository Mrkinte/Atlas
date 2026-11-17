using System.Text;
using Atlas.Extensions;
using Atlas.Features.Auth;

namespace Atlas.Commands;

internal static class ResetPasswordCommand
{
    public static async Task<int> RunAsync(
        AuthService authService,
        AppInitializer initializer,
        AtlasPaths paths,
        string? databaseFile,
        string? username,
        TextWriter output,
        TextWriter error,
        CancellationToken ct)
    {
        if (Console.IsInputRedirected)
        {
            error.WriteLine("错误: 重置密码需要在交互式终端中执行。");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            output.Write("请输入用户名：");
            username = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                error.WriteLine("错误: 用户名不能为空。");
                return 2;
            }
        }

        string password;

        try
        {
            password = ReadPassword(output);
        }
        catch (InvalidOperationException ex)
        {
            error.WriteLine($"错误: {ex.Message}");
            return 1;
        }

        return await ResetAsync(
            authService, initializer, paths, databaseFile,
            username, password, output, error, ct);
    }

    public static async Task<int> ResetAsync(
        AuthService authService,
        AppInitializer initializer,
        AtlasPaths paths,
        string? databaseFile,
        string username,
        string password,
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

        initializer.EnsureSchema();

        var user = await authService.FindByUsernameAsync(username.Trim(), ct);

        if (user is null)
        {
            error.WriteLine($"错误: 用户 {username} 不存在。");
            error.WriteLine($"数据目录: {paths.DataDirectory}（来源: {CliHelp.DescribeDataSource(paths.Source)}）");
            if (databaseFile is not null) error.WriteLine($"数据库: {databaseFile}");
            return 1;
        }

        if (!PasswordPolicy.IsValid(password))
        {
            error.WriteLine("错误: 新密码不符合要求：至少 8 个字符，且 UTF-8 不超过 72 字节。");
            return 1;
        }

        await authService.ChangeCredentialAsync(user.Id, user.PasswordVersion, password, ct: ct);

        output.WriteLine($"已重置用户 {user.Username} 的密码，相关登录状态已全部失效。");
        return 0;
    }

    private static string ReadPassword(TextWriter output)
    {
        output.Write("请输入新密码（输入不回显）：");
        var password = ReadHiddenLine();
        output.Write("请再次输入新密码：");
        var confirm = ReadHiddenLine();

        if (!string.Equals(password, confirm, StringComparison.Ordinal))
            throw new InvalidOperationException("两次输入的密码不一致。");

        return password;
    }

    private static string ReadHiddenLine()
    {
        var buffer = new StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0) buffer.Length--;
                continue;
            }

            // Unix 下 Ctrl+D 表示输入结束
            if (key.KeyChar == '\u0004') break;

            if (!char.IsControl(key.KeyChar)) buffer.Append(key.KeyChar);
        }

        return buffer.ToString();
    }
}
