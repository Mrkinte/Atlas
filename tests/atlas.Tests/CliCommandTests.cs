using Atlas.Commands;
using Atlas.Features.Auth.Entities;
using Atlas.Features.Demo;
using Atlas.Tests.TestSupport;
using Microsoft.Extensions.Configuration;

namespace Atlas.Tests;

public class CliCommandTests
{
    [Fact]
    public async Task 用户信息列出账号()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        host.Fsql.Insert(new UserEntity
        {
            Username = "atlas",
            PasswordHash = "$2a$12$abcdefghijklmnopqrstuv",
            PasswordVersion = 1
        }).ExecuteAffrows();

        var output = new StringWriter();
        var error = new StringWriter();

        var exit = await UserInfoCommand.RunAsync(
            host.CreateAuthService(), CreateDemoMode(), host.Paths, host.DatabaseFile,
            output, error, default);

        Assert.Equal(0, exit);
        Assert.Contains("atlas", output.ToString());
        Assert.DoesNotContain("$2a$", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task 数据库不存在时不创建空库()
    {
        using var host = new TestHost();
        var missing = Path.Combine(host.DataDirectory, "missing", "atlas.db");
        var error = new StringWriter();

        var exit = await UserInfoCommand.RunAsync(
            host.CreateAuthService(), CreateDemoMode(), host.Paths, missing,
            new StringWriter(), error, default);

        Assert.Equal(1, exit);
        Assert.False(File.Exists(missing));
        Assert.Contains("数据库不存在", error.ToString());
    }

    [Fact]
    public async Task 空库提示没有账号()
    {
        using var host = new TestHost();
        host.InitializeSchema();

        var output = new StringWriter();

        var exit = await UserInfoCommand.RunAsync(
            host.CreateAuthService(), CreateDemoMode(), host.Paths, host.DatabaseFile,
            output, new StringWriter(), default);

        Assert.Equal(0, exit);
        Assert.Contains("还没有任何账号", output.ToString());
    }

    [Fact]
    public async Task 重置密码成功并作废旧令牌()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        host.Fsql.Insert(new UserEntity
        {
            Username = "atlas",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("old-password", 12)
        }).ExecuteAffrows();

        var authService = host.CreateAuthService();
        var output = new StringWriter();
        var error = new StringWriter();

        var exit = await ResetPasswordCommand.ResetAsync(
            authService, host.Initializer, host.Paths, host.DatabaseFile,
            "atlas", "new-password-123", output, error, default);

        Assert.Equal(0, exit);
        Assert.Contains("已重置用户 atlas", output.ToString());

        var user = await authService.FindByUsernameAsync("atlas");
        Assert.NotNull(user);
        Assert.True(BCrypt.Net.BCrypt.Verify("new-password-123", user!.PasswordHash));
        Assert.Equal(1, user.PasswordVersion);
    }

    [Fact]
    public async Task 重置密码用户不存在()
    {
        using var host = new TestHost();
        host.InitializeSchema();

        var error = new StringWriter();

        var exit = await ResetPasswordCommand.ResetAsync(
            host.CreateAuthService(), host.Initializer, host.Paths, host.DatabaseFile,
            "nobody", "new-password-123", new StringWriter(), error, default);

        Assert.Equal(1, exit);
        Assert.Contains("不存在", error.ToString());
    }

    [Fact]
    public async Task 重置密码拒绝过短密码()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        var original = BCrypt.Net.BCrypt.HashPassword("old-password", 12);
        host.Fsql.Insert(new UserEntity { Username = "atlas", PasswordHash = original }).ExecuteAffrows();

        var authService = host.CreateAuthService();
        var error = new StringWriter();

        var exit = await ResetPasswordCommand.ResetAsync(
            authService, host.Initializer, host.Paths, host.DatabaseFile,
            "atlas", "short", new StringWriter(), error, default);

        Assert.Equal(1, exit);
        Assert.Contains("不符合要求", error.ToString());

        var user = await authService.FindByUsernameAsync("atlas");
        Assert.Equal(original, user!.PasswordHash);
        Assert.Equal(0, user.PasswordVersion);
    }

    private static DemoMode CreateDemoMode()
    {
        return new DemoMode(new ConfigurationBuilder().Build());
    }
}
