using System.Text.Json.Serialization;
using Atlas.Features.Demo.Contracts;

namespace Atlas.Features.Demo;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DemoStatusResponse))]
internal partial class DemoJsonContext : JsonSerializerContext;

public static class DemoJsonContextRegistration
{
    public static IServiceCollection AddDemoJsonContext(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, DemoJsonContext.Default));
        return services;
    }
}