using System.Text.Json.Serialization;

namespace Atlas.Features.Update;

/// <summary>GitHub Release 响应，只映射检查更新用到的字段</summary>
internal sealed record GitHubRelease(
    [property: JsonPropertyName("tag_name")] string? TagName,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("prerelease")] bool? Prerelease,
    [property: JsonPropertyName("published_at")] DateTimeOffset? PublishedAt);
