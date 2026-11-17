using System.Buffers;
using System.Text;
using System.Text.Json;
using Atlas.Extensions;
using Atlas.Features.Update.Contracts;

namespace Atlas.Features.Update;

/// <summary>
///     查询 GitHub 最新 Release。结果在进程内缓存，
///     避免每次打开设置页都去拉取
/// </summary>
public sealed class UpdateService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<UpdateService> logger)
{
    /// <summary>正常响应只有几 KB，异常响应不能无限读入内存</summary>
    private const int MaxResponseBytes = 512 * 1024;

    private const int MaxNotesLength = 8000;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    /// <summary>手动检查绕过缓存，但保留最小间隔，避免连续点击</summary>
    private static readonly TimeSpan ForceInterval = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan FailureInterval = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);

    private readonly SemaphoreSlim _gate = new(1, 1);

    private UpdateCheckResponse? _cached;
    private DateTimeOffset _fetchedAt = DateTimeOffset.MinValue;
    private string? _lastFailure;
    private DateTimeOffset _failedAt = DateTimeOffset.MinValue;

    public async Task<UpdateCheckResponse> CheckAsync(bool force, CancellationToken ct)
    {
        if (IsFresh(force)) return _cached!;

        await _gate.WaitAsync(ct);
        try
        {
            if (IsFresh(force)) return _cached!;

            // 网络不通时每次进设置页都要等超时，短时间内的重复失败直接复用
            if (!force && _lastFailure is not null
                && DateTimeOffset.UtcNow - _failedAt < FailureInterval)
                throw new UpdateUnavailableException(_lastFailure);

            try
            {
                return await FetchAsync(ct);
            }
            catch (UpdateUnavailableException ex)
            {
                _lastFailure = ex.Message;
                _failedAt = DateTimeOffset.UtcNow;
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsFresh(bool force)
    {
        if (_cached is null) return false;

        var age = DateTimeOffset.UtcNow - _fetchedAt;
        return force ? age < ForceInterval : age < CacheDuration;
    }

    private async Task<UpdateCheckResponse> FetchAsync(CancellationToken ct)
    {
        var (owner, repository) = ResolveRepository();

        var client = httpClientFactory.CreateClient();
        client.Timeout = RequestTimeout;

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.github.com/repos/"
            + $"{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}"
            + "/releases/latest");
        request.Headers.UserAgent.ParseAdd("atlas-update-check");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await SendAsync(client, request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            logger.LogWarning("检查更新失败：GitHub 返回 {Status}", status);

            throw new UpdateUnavailableException(status switch
            {
                // GitHub 在仓库不存在或没有发布 Release 时均返回 404
                StatusCodes.Status404NotFound => "未找到 GitHub 仓库或尚未发布 Release",
                StatusCodes.Status403Forbidden or StatusCodes.Status429TooManyRequests =>
                    "GitHub 请求过于频繁，请稍后再试",
                _ => $"GitHub 返回 {status}"
            });
        }

        var body = await ReadBodyAsync(response, ct);
        if (body is null)
        {
            logger.LogWarning("检查更新失败：响应超过 {Limit} 字节", MaxResponseBytes);
            throw new UpdateUnavailableException("更新信息异常，已放弃解析");
        }

        var release = ParseRelease(body);

        if (!ReleaseVersion.TryParse(release.TagName, out var latestVersion))
        {
            logger.LogWarning("检查更新失败：无法识别的 tag {Tag}", release.TagName);
            throw new UpdateUnavailableException("更新版本号无法识别");
        }

        // 本地版本还没有可解析的数字段（比如首次构建的 0.0.0-dev）时，任何 Release 都算更新
        _ = ReleaseVersion.TryParse(AppVersion.Current, out var currentVersion);

        var tag = release.TagName!;
        _lastFailure = null;
        _fetchedAt = DateTimeOffset.UtcNow;

        _cached = new UpdateCheckResponse(
            AppVersion.Current,
            NormalizeTag(tag),
            latestVersion.CompareTo(currentVersion) > 0,
            release.Prerelease == true,
            release.PublishedAt,
            TruncateNotes(release.Body),
            BuildReleaseUrl(owner, repository, tag),
            _fetchedAt);

        return _cached;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("检查更新失败：连接 GitHub 超时");
            throw new UpdateUnavailableException("连接 GitHub 超时，请检查网络");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "检查更新失败：无法连接 GitHub");
            throw new UpdateUnavailableException("无法连接 GitHub，请检查网络");
        }
    }

    private static async Task<string?> ReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        var buffer = ArrayPool<byte>.Shared.Rent(MaxResponseBytes);
        try
        {
            var total = 0;
            while (total < MaxResponseBytes)
            {
                var read = await stream.ReadAsync(
                    buffer.AsMemory(total, MaxResponseBytes - total), ct);
                if (read == 0) return Encoding.UTF8.GetString(buffer, 0, total);

                total += read;
            }

            // 读满上限仍未结束，说明响应异常偏大
            return null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private GitHubRelease ParseRelease(string body)
    {
        GitHubRelease? release;

        try
        {
            release = JsonSerializer.Deserialize(body, UpdateJsonContext.Default.GitHubRelease);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "检查更新失败：响应不是有效 JSON");
            throw new UpdateUnavailableException("更新信息格式不正确");
        }

        if (release?.TagName is null or "")
        {
            logger.LogWarning("检查更新失败：响应缺少 tag_name");
            throw new UpdateUnavailableException("更新信息缺少版本号");
        }

        return release;
    }

    private (string Owner, string Repository) ResolveRepository()
    {
        var configured = configuration["Atlas:Update:Repository"];

        if (string.IsNullOrWhiteSpace(configured))
        {
            logger.LogWarning("检查更新失败：未配置 Atlas:Update:Repository");
            throw new UpdateUnavailableException("未配置 GitHub 仓库");
        }

        var parts = configured.Split(
            '/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length != 2)
        {
            logger.LogWarning("GitHub 仓库配置无效：{Repository}", configured);
            throw new UpdateUnavailableException("GitHub 仓库配置无效，应为 owner/repo");
        }

        return (parts[0], parts[1]);
    }

    private static string BuildReleaseUrl(string owner, string repository, string tag)
    {
        return "https://github.com/"
               + $"{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}"
               + $"/releases/tag/{Uri.EscapeDataString(tag)}";
    }

    private static string NormalizeTag(string tag)
    {
        var trimmed = tag.Trim();
        return trimmed.Length > 0 && trimmed[0] is 'v' or 'V' ? trimmed[1..] : trimmed;
    }

    private static string? TruncateNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;

        var trimmed = notes.Trim();
        return trimmed.Length <= MaxNotesLength ? trimmed : trimmed[..MaxNotesLength] + "…";
    }
}
