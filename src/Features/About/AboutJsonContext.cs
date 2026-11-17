using System.Text.Json.Serialization;
using Atlas.Features.About.Contracts;

namespace Atlas.Features.About;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AboutResponse))]
internal partial class AboutJsonContext : JsonSerializerContext;

public static class AboutJsonContextRegistration
{
    public static IServiceCollection AddAboutJsonContext(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, AboutJsonContext.Default));
        return services;
    }
}