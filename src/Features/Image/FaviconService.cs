using System.Buffers;
using System.Text;
using Atlas.Features.Image.Contracts;

namespace Atlas.Features.Image;

/// <summary>
///     抓取目标网页声明的图标并保存到本地图标库：请求页面 → 解析 icon link → 下载 → 落盘入库
/// </summary>
public class FaviconService(
    IHttpClientFactory httpClientFactory,
    ImageService imageService)
{
    /// <summary>1 MB</summary>
    private const int MaxIconBytes = 1024 * 1024;

    /// <summary>512 KB，避免异常页面占用内存</summary>
    private const int MaxHtmlBytes = 512 * 1024;

    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/120.0 Safari/537.36";

    /// <summary>SVG 以静态文件返回时由 CSP 禁止脚本执行</summary>
    private static readonly HashSet<string> AllowedIconExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".svg", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".ico"
        };

    public async Task<ImageResponse?> FetchAndSaveAsync(
        string pageUrl, CancellationToken ct)
    {
        if (!TryNormalizeUrl(pageUrl, out var page)) return null;

        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

        var (html, finalUri) = await GetHtmlAsync(client, page, ct);

        // 多个图标按清晰度优先：SVG > PNG > WebP > 其他位图 > ICO，同类型下尺寸大的优先，
        // 避免拿到 16x16 的低清 .ico
        var candidates = html is null
            ? new List<string>()
            : ExtractIconCandidates(html, finalUri)
                .OrderBy(c => GetTypeRank(c.Url, c.Type))
                .ThenByDescending(c => c.Size)
                .Select(c => c.Url)
                .ToList();

        // 兜底：页面未声明可下载的图标时尝试站点根目录的 favicon.ico
        candidates.Add(new Uri(finalUri, "/favicon.ico").ToString());

        var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (!tried.Add(candidate)) continue;

            var saved = await TryDownloadAsync(client, candidate, ct);
            if (saved is not null) return saved;
        }

        return null;
    }

    private static bool TryNormalizeUrl(string value, out Uri uri)
    {
        uri = null!;

        var trimmed = value.Trim();
        if (trimmed.Length == 0) return false;

        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            trimmed = "http://" + trimmed;

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            return false;

        uri = parsed;
        return true;
    }

    private static async Task<(string? Html, Uri FinalUri)> GetHtmlAsync(
        HttpClient client, Uri page, CancellationToken ct)
    {
        try
        {
            using var response = await client.GetAsync(page, ct);
            if (!response.IsSuccessStatusCode) return (null, page);

            var finalUri = response.RequestMessage?.RequestUri ?? page;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);

            // 每次最多读 512 KB，用池化缓冲避免重复分配大对象
            var buffer = ArrayPool<byte>.Shared.Rent(MaxHtmlBytes);

            try
            {
                var limit = Math.Min(MaxHtmlBytes, buffer.Length);
                var total = 0;

                while (total < limit)
                {
                    var read = await stream.ReadAsync(
                        buffer.AsMemory(total, limit - total), ct);
                    if (read == 0) break;
                    total += read;
                }

                return (Encoding.UTF8.GetString(buffer, 0, total), finalUri);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return (null, page);
        }
    }

    private static IEnumerable<IconCandidate> ExtractIconCandidates(string html, Uri baseUri)
    {
        var results = new List<IconCandidate>();
        var searchFrom = 0;

        while (true)
        {
            var start = html.IndexOf("<link", searchFrom, StringComparison.OrdinalIgnoreCase);
            if (start < 0) break;

            var end = html.IndexOf('>', start);
            if (end < 0) break;

            var tag = html.AsSpan(start, end - start + 1);
            searchFrom = end + 1;

            if (!TryGetAttribute(tag, "rel", out var rel)
                || !TryGetAttribute(tag, "href", out var href))
                continue;

            if (!rel.Contains("icon", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(href))
                continue;

            // mask-icon 是 Safari 的工具栏单色图标，不适合作为站点图标
            if (rel.Contains("mask-icon", StringComparison.OrdinalIgnoreCase)) continue;

            if (!Uri.TryCreate(baseUri, href.Trim(), out var iconUri)
                || (iconUri.Scheme != Uri.UriSchemeHttp && iconUri.Scheme != Uri.UriSchemeHttps))
                continue;

            TryGetAttribute(tag, "type", out var type);
            TryGetAttribute(tag, "sizes", out var sizes);

            results.Add(new IconCandidate(
                iconUri.ToString(),
                type,
                ParseMaxSize(sizes)));
        }

        return results;
    }

    private static int GetTypeRank(string url, string type)
    {
        var mediaType = type.ToLowerInvariant();
        var path = Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.AbsolutePath.ToLowerInvariant()
            : url.ToLowerInvariant();

        if (mediaType.Contains("svg") || path.EndsWith(".svg")) return 0;
        if (mediaType.Contains("png") || path.EndsWith(".png")) return 1;
        if (mediaType.Contains("webp") || path.EndsWith(".webp")) return 2;
        if (mediaType.Contains("icon") || path.EndsWith(".ico")) return 4;

        return 3;
    }

    /// <summary>从 "16x16 32x32" 或 "any" 这类 sizes 属性中取最大边长</summary>
    private static int ParseMaxSize(string sizes)
    {
        var max = 0;

        foreach (var part in sizes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = part.IndexOf('x');
            if (separatorIndex <= 0) continue;

            if (int.TryParse(part[..separatorIndex], out var width) && width > max) max = width;
        }

        return max;
    }

    private static bool TryGetAttribute(
        ReadOnlySpan<char> tag, string name, out string value)
    {
        value = string.Empty;
        var remaining = tag;

        while (true)
        {
            var index = remaining.IndexOf(name, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return false;

            var rest = remaining[(index + name.Length)..].TrimStart();
            var precededBySpace = index == 0 || char.IsWhiteSpace(remaining[index - 1]);

            if (precededBySpace && !rest.IsEmpty && rest[0] == '=')
            {
                rest = rest[1..].TrimStart();
                if (!rest.IsEmpty && (rest[0] == '"' || rest[0] == '\''))
                {
                    var quote = rest[0];
                    var close = rest[1..].IndexOf(quote);
                    if (close < 0) return false;

                    value = rest.Slice(1, close).ToString();
                    return true;
                }
            }

            remaining = remaining[(index + name.Length)..];
        }
    }

    private async Task<ImageResponse?> TryDownloadAsync(
        HttpClient client, string iconUrl, CancellationToken ct)
    {
        try
        {
            using var response = await client.GetAsync(
                iconUrl, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode) return null;
            if (response.Content.Headers.ContentLength > MaxIconBytes) return null;

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is not null
                && mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                return null;

            var extension = ResolveExtension(iconUrl, mediaType);
            if (extension is null) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);

            // 最多 1 MB，用池化缓冲累积，避免中间大对象
            var buffer = ArrayPool<byte>.Shared.Rent(MaxIconBytes);

            try
            {
                var limit = Math.Min(MaxIconBytes, buffer.Length);
                var total = 0;

                while (total < limit)
                {
                    var read = await stream.ReadAsync(
                        buffer.AsMemory(total, limit - total), ct);
                    if (read == 0) break;
                    total += read;
                }

                if (total == 0) return null;

                // 缓冲区写满时再探一个字节，超过上限的响应直接丢弃
                var probe = new byte[1];
                if (total >= limit && await stream.ReadAsync(probe, 0, 1, ct) > 0) return null;

                var originalName = Path.GetFileName(new Uri(iconUrl).LocalPath);
                if (string.IsNullOrWhiteSpace(originalName)) originalName = $"favicon{extension}";

                // 直接包住池化缓冲，避免复制进 MemoryStream
                using var content = new MemoryStream(buffer, 0, total, false);
                return await imageService.SaveIconAsync(
                    content, extension, originalName, ct);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException
                                      or UriFormatException)
        {
            return null;
        }
    }

    private static string? ResolveExtension(string iconUrl, string? mediaType)
    {
        if (Uri.TryCreate(iconUrl, UriKind.Absolute, out var uri))
        {
            var extension = Path.GetExtension(uri.AbsolutePath);
            if (AllowedIconExtensions.Contains(extension)) return extension.ToLowerInvariant();
        }

        return mediaType?.ToLowerInvariant() switch
        {
            "image/svg+xml" => ".svg",
            "image/png" or "image/apng" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            "image/x-icon" or "image/vnd.microsoft.icon" => ".ico",
            _ => null
        };
    }

    private sealed record IconCandidate(string Url, string Type, int Size);
}