using System.Text.Json.Serialization;
using Atlas.Features.Https.Contracts;

namespace Atlas.Features.Https;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(HttpsConfig))]
[JsonSerializable(typeof(HttpsStatusResponse))]
[JsonSerializable(typeof(UpdateHttpsRequest))]
[JsonSerializable(typeof(CertificateInfo))]
internal partial class HttpsJsonContext : JsonSerializerContext;

public static class HttpsJsonContextRegistration
{
    public static IServiceCollection AddHttpsJsonContext(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, HttpsJsonContext.Default));
        return services;
    }
}