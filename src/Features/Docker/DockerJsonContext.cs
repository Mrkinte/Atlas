using System.Text.Json.Serialization;
using Atlas.Features.Docker.Contracts;

namespace Atlas.Features.Docker;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DockerStatusResponse))]
[JsonSerializable(typeof(DockerContainerInfo))]
[JsonSerializable(typeof(DockerApiContainer[]))]
[JsonSerializable(typeof(DockerApiInfo))]
[JsonSerializable(typeof(DockerApiStats))]
internal partial class DockerJsonContext : JsonSerializerContext;

public static class DockerJsonContextRegistration
{
    public static IServiceCollection AddDockerJsonContext(
        this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(
                0, DockerJsonContext.Default));
        return services;
    }
}
