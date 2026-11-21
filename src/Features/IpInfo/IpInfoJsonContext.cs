using System.Text.Json.Serialization;
using Atlas.Features.IpInfo.Contracts;

namespace Atlas.Features.IpInfo;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(IpInfoResponse))]
internal partial class IpInfoJsonContext : JsonSerializerContext;

public static class IpInfoJsonContextRegistration
{
    public static IServiceCollection AddIpInfoJsonContext(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, IpInfoJsonContext.Default));
        return services;
    }
}
