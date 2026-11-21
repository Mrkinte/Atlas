namespace Atlas.Features.Docker.Contracts;

public sealed record DockerStatusResponse(
    bool Enabled,
    bool Available,
    bool DetailsVisible,
    int Total,
    int Running,
    int Paused,
    int Stopped,
    int Images,
    double? CpuPercent,
    long? MemoryBytes,
    DateTimeOffset SampledAt,
    DockerContainerInfo[] Containers)
{
    public static DockerStatusResponse Disabled()
    {
        return new DockerStatusResponse(
            false, false, false, 0, 0, 0, 0, 0, null, null,
            DateTimeOffset.UtcNow, []);
    }
}

public sealed record DockerContainerInfo(
    string Id,
    string Name,
    string Image,
    string State,
    string Status);
