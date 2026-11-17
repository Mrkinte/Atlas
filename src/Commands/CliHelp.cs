using Atlas.Extensions;

namespace Atlas.Commands;

internal static class CliHelp
{
    public static void WriteVersion(TextWriter output)
    {
        output.WriteLine($"atlas {AppVersion.Current}");
    }

    public static void WriteGeneral(TextWriter output)
    {
        output.WriteLine($"Atlas {AppVersion.Current}");
        output.WriteLine();
        output.WriteLine("用法:");
        output.WriteLine("  atlas                             启动 Web 服务");
        output.WriteLine("  atlas --user-info                 查看账号信息");
        output.WriteLine("  atlas --reset-password [用户名]   重置账号密码");
        output.WriteLine("  atlas --help                      显示帮助");
        output.WriteLine("  atlas --version                   显示版本");
        output.WriteLine();
        output.WriteLine("全局选项:");
        output.WriteLine("  -h, --help              显示帮助");
        output.WriteLine("  -V, --version           显示版本");
        output.WriteLine("      --data-dir <路径>   指定数据目录，优先级最高");
        output.WriteLine();
        output.WriteLine("用户命令:");
        output.WriteLine("      --user-info                     列出数据库中的账号");
        output.WriteLine("      --reset-password [用户名]       在交互式终端中重置密码");
        output.WriteLine("      --user <用户名>                 指定账号（与 --reset-password 搭配）");
        output.WriteLine();
        output.WriteLine("数据目录解析顺序（从高到低）:");
        output.WriteLine("  1. --data-dir <路径>");
        output.WriteLine("  2. 环境变量 ATLAS_DATA_DIR");
        output.WriteLine("  3. 配置项 Atlas:DataDirectory（appsettings.json 或 Atlas__DataDirectory）");
        output.WriteLine("  4. 程序所在目录");
        output.WriteLine();
        output.WriteLine("服务选项:");
        output.WriteLine("  --urls <地址>           监听地址，默认 http://localhost:5000");
        output.WriteLine("  其余 ASP.NET Core 参数原样传递");
        output.WriteLine();
        output.WriteLine("示例:");
        output.WriteLine("  atlas --user-info");
        output.WriteLine("  atlas --user-info --data-dir /var/lib/atlas");
        output.WriteLine("  atlas --reset-password atlas");
        output.WriteLine();
        output.WriteLine("退出码:");
        output.WriteLine("  0  成功");
        output.WriteLine("  1  执行失败");
        output.WriteLine("  2  参数错误");
    }

    public static void WriteCommand(CliCommand command, TextWriter output)
    {
        switch (command)
        {
            case CliCommand.UserInfo:
                output.WriteLine("用法: atlas --user-info [--data-dir <路径>]");
                output.WriteLine();
                output.WriteLine("列出数据库中的账号、数据目录和数据库路径，不修改任何数据。");
                break;

            case CliCommand.ResetPassword:
                output.WriteLine("用法: atlas --reset-password [用户名] [选项]");
                output.WriteLine();
                output.WriteLine("在交互式终端中重置指定账号的密码。用户名可省略，会提示输入。");
                output.WriteLine();
                output.WriteLine("选项:");
                output.WriteLine("  --user <用户名>      指定账号");
                output.WriteLine("  --data-dir <路径>    指定数据目录");
                output.WriteLine();
                output.WriteLine("密码要求: 至少 8 个字符，UTF-8 不超过 72 字节。");
                break;

            default:
                WriteGeneral(output);
                break;
        }
    }

    public static string DescribeDataSource(DataDirectorySource source)
    {
        return source switch
        {
            DataDirectorySource.CommandLine => "命令行 --data-dir",
            DataDirectorySource.Environment => "环境变量 ATLAS_DATA_DIR",
            DataDirectorySource.Configuration => "配置项 Atlas:DataDirectory",
            _ => "默认（程序目录）"
        };
    }
}
