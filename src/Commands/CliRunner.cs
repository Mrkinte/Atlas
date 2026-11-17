using Atlas.Extensions;
using Atlas.Features.Auth;
using Atlas.Features.Demo;

namespace Atlas.Commands;

internal static class CliRunner
{
    public static async Task<int> RunAsync(CliInvocation invocation, IServiceProvider services)
    {
        try
        {
            using var scope = services.CreateScope();
            var provider = scope.ServiceProvider;

            var paths = provider.GetRequiredService<AtlasPaths>();
            var configuration = provider.GetRequiredService<IConfiguration>();

            var databaseFile = DatabaseConnection.GetFilePath(
                configuration.GetConnectionString("Atlas"), paths.DataDirectory);

            return invocation.Command switch
            {
                CliCommand.UserInfo => await UserInfoCommand.RunAsync(
                    provider.GetRequiredService<AuthService>(),
                    provider.GetRequiredService<DemoMode>(),
                    paths,
                    databaseFile,
                    Console.Out,
                    Console.Error,
                    CancellationToken.None),

                CliCommand.ResetPassword => await ResetPasswordCommand.RunAsync(
                    provider.GetRequiredService<AuthService>(),
                    provider.GetRequiredService<AppInitializer>(),
                    paths,
                    databaseFile,
                    invocation.Username,
                    Console.Out,
                    Console.Error,
                    CancellationToken.None),

                _ => 0
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"错误: {ex.Message}");
            return 1;
        }
    }
}
