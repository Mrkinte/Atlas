using System.Text.Json.Serialization;
using Atlas.Features.SystemStatus.Contracts;

namespace Atlas.Features.SystemStatus;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SystemStatusMessage))]
[JsonSerializable(typeof(SystemStatusDevice))]
[JsonSerializable(typeof(SystemStatusHardware))]
internal partial class SystemStatusJsonContext : JsonSerializerContext;

public static class SystemStatusJsonContextRegistration
{
    public static IServiceCollection AddSystemStatusJsonContext(
        this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(
                0, SystemStatusJsonContext.Default));
        return services;
    }
}