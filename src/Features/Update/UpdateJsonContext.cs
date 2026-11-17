using System.Text.Json.Serialization;
using Atlas.Features.Update.Contracts;

namespace Atlas.Features.Update;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UpdateCheckResponse))]
[JsonSerializable(typeof(GitHubRelease))]
internal partial class UpdateJsonContext : JsonSerializerContext;

public static class UpdateJsonContextRegistration
{
    public static IServiceCollection AddUpdateJsonContext(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, UpdateJsonContext.Default));
        return services;
    }
}
