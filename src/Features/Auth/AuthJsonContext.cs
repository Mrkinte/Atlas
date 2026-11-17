using System.Text.Json.Serialization;
using Atlas.Features.Auth.Contracts;

namespace Atlas.Features.Auth;

[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(AuthStatusResponse))]
[JsonSerializable(typeof(ResetPasswordRequest))]
[JsonSerializable(typeof(ChangeUsernameRequest))]
internal partial class AuthJsonContext : JsonSerializerContext;

public static class AuthJsonContextRegistration
{
    public static IServiceCollection AddAuthJsonContext(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, AuthJsonContext.Default));
        return services;
    }
}