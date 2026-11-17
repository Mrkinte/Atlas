namespace Atlas.Commands;

internal enum CliCommand
{
    None,
    Help,
    Version,
    UserInfo,
    ResetPassword
}

internal sealed class CliUsageException(string message) : Exception(message);

internal sealed record CliInvocation(
    CliCommand Command,
    string? DataDirectory,
    string? Username,
    CliCommand HelpTopic,
    string[] ServerArgs)
{
    public bool HasCommand => Command is CliCommand.UserInfo or CliCommand.ResetPassword;

    public static CliInvocation Parse(string[] args)
    {
        CliCommand? command = null;
        string? dataDirectory = null;
        string? username = null;
        string? positionalUsername = null;
        var helpRequested = false;
        var versionRequested = false;
        var serverArgs = new List<string>();

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];

            switch (arg)
            {
                case "-h":
                case "--help":
                    helpRequested = true;
                    continue;
                case "-V":
                case "--version":
                    versionRequested = true;
                    continue;
                case "--data-dir":
                    dataDirectory = RequireValue(args, ref index, arg);
                    continue;
                case "--user":
                    username = RequireValue(args, ref index, arg);
                    continue;
                case "--user-info":
                    command = SetCommand(command, CliCommand.UserInfo);
                    continue;
                case "--reset-password":
                    command = SetCommand(command, CliCommand.ResetPassword);
                    continue;
            }

            if (arg.StartsWith("--data-dir=", StringComparison.Ordinal))
            {
                dataDirectory = ValueAfterEquals(arg, "--data-dir");
                continue;
            }

            if (arg.StartsWith("--user=", StringComparison.Ordinal))
            {
                username = ValueAfterEquals(arg, "--user");
                continue;
            }

            // --reset-password 后的第一个非选项参数按用户名处理
            if (command == CliCommand.ResetPassword && positionalUsername is null && !arg.StartsWith('-'))
            {
                positionalUsername = arg;
                continue;
            }

            if (command is null)
            {
                serverArgs.Add(arg);
                continue;
            }

            throw new CliUsageException($"未知参数：{arg}");
        }

        if (positionalUsername is not null)
        {
            if (username is not null && !string.Equals(username, positionalUsername, StringComparison.Ordinal))
                throw new CliUsageException("用户名不能同时通过位置参数和 --user 指定");

            username = positionalUsername;
        }

        if (command != CliCommand.ResetPassword && username is not null)
            throw new CliUsageException("--user 只能与 --reset-password 一起使用");

        if (helpRequested)
            return new CliInvocation(
                CliCommand.Help, dataDirectory, username,
                command ?? CliCommand.None, []);

        if (versionRequested)
            return new CliInvocation(
                CliCommand.Version, dataDirectory, username,
                CliCommand.None, []);

        return new CliInvocation(
            command ?? CliCommand.None, dataDirectory, username,
            CliCommand.None, serverArgs.ToArray());
    }

    private static CliCommand SetCommand(CliCommand? current, CliCommand value)
    {
        if (current is not null && current != value)
            throw new CliUsageException("--user-info 与 --reset-password 不能同时使用");

        return value;
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
            throw new CliUsageException($"选项 {option} 缺少参数值");

        return args[++index];
    }

    private static string ValueAfterEquals(string arg, string option)
    {
        var value = arg[(option.Length + 1)..];
        if (string.IsNullOrWhiteSpace(value))
            throw new CliUsageException($"选项 {option} 缺少参数值");

        return value;
    }
}
