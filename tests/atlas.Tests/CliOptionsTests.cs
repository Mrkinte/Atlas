using Atlas.Commands;

namespace Atlas.Tests;

public class CliOptionsTests
{
    [Fact]
    public void 帮助参数()
    {
        Assert.Equal(CliCommand.Help, CliInvocation.Parse(["--help"]).Command);
        Assert.Equal(CliCommand.Help, CliInvocation.Parse(["-h"]).Command);
    }

    [Fact]
    public void 版本参数()
    {
        Assert.Equal(CliCommand.Version, CliInvocation.Parse(["--version"]).Command);
        Assert.Equal(CliCommand.Version, CliInvocation.Parse(["-V"]).Command);
    }

    [Fact]
    public void 命令帮助带主题()
    {
        var invocation = CliInvocation.Parse(["--reset-password", "--help"]);

        Assert.Equal(CliCommand.Help, invocation.Command);
        Assert.Equal(CliCommand.ResetPassword, invocation.HelpTopic);
    }

    [Fact]
    public void 用户信息命令()
    {
        var invocation = CliInvocation.Parse(["--user-info"]);

        Assert.Equal(CliCommand.UserInfo, invocation.Command);
        Assert.True(invocation.HasCommand);
    }

    [Fact]
    public void 重置密码支持位置用户名()
    {
        var invocation = CliInvocation.Parse(["--reset-password", "atlas"]);

        Assert.Equal(CliCommand.ResetPassword, invocation.Command);
        Assert.Equal("atlas", invocation.Username);
    }

    [Fact]
    public void 重置密码支持user选项()
    {
        var invocation = CliInvocation.Parse(["--reset-password", "--user", "atlas"]);

        Assert.Equal(CliCommand.ResetPassword, invocation.Command);
        Assert.Equal("atlas", invocation.Username);
    }

    [Fact]
    public void 用户名冲突时报错()
    {
        Assert.Throws<CliUsageException>(() =>
            CliInvocation.Parse(["--reset-password", "atlas", "--user", "admin"]));
    }

    [Fact]
    public void 旧的位置命令不再识别()
    {
        var invocation = CliInvocation.Parse(["reset-password", "atlas"]);

        Assert.Equal(CliCommand.None, invocation.Command);
        Assert.Equal(["reset-password", "atlas"], invocation.ServerArgs);
    }

    [Fact]
    public void 数据目录支持两种写法()
    {
        Assert.Equal("/tmp/atlas", CliInvocation.Parse(["--data-dir", "/tmp/atlas"]).DataDirectory);
        Assert.Equal("/tmp/atlas", CliInvocation.Parse(["--data-dir=/tmp/atlas"]).DataDirectory);
    }

    [Fact]
    public void 数据目录缺少值时报错()
    {
        Assert.Throws<CliUsageException>(() => CliInvocation.Parse(["--data-dir"]));
    }

    [Fact]
    public void user选项必须配合重置密码()
    {
        Assert.Throws<CliUsageException>(() => CliInvocation.Parse(["--user", "atlas"]));
    }

    [Fact]
    public void 用户命令不能同时使用()
    {
        Assert.Throws<CliUsageException>(() =>
            CliInvocation.Parse(["--user-info", "--reset-password"]));
    }

    [Fact]
    public void 服务参数原样传递()
    {
        var invocation = CliInvocation.Parse(["--urls", "http://0.0.0.0:5000"]);

        Assert.Equal(CliCommand.None, invocation.Command);
        Assert.Equal(["--urls", "http://0.0.0.0:5000"], invocation.ServerArgs);
    }
}
