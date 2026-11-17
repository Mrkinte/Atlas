namespace Atlas.Features.About.Contracts;

public record AboutResponse(
    string Version,
    string Runtime,
    DateTimeOffset StartedAt,
    long UptimeSeconds,
    long DatabaseBytes,
    int CardCount,
    int ImageCount,
    string DataDirectory,
    AboutMemoryInfo Memory);