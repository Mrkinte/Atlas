namespace Atlas.Features.Update.Contracts;

public record UpdateCheckResponse(
    string CurrentVersion,
    string LatestVersion,
    bool UpdateAvailable,
    bool IsPrerelease,
    DateTimeOffset? PublishedAt,
    string? ReleaseNotes,
    string ReleaseUrl,
    DateTimeOffset CheckedAt);
