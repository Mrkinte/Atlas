using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using Atlas.Features.IpInfo.Contracts;

namespace Atlas.Features.IpInfo;

/// <summary>
///     进程内缓存公网地址与归属地查询结果，避免频繁调用第三方API
/// </summary>
public sealed class IpInfoService(
    IHttpClientFactory httpClientFactory,
    ILogger<IpInfoService> logger)
{
    /// <summary>单次响应只有几 KB，超出说明拿到了错误页</summary>
    private const int MaxResponseBytes = 64 * 1024;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan ForceInterval = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(4);

    private static readonly TimeSpan LatencyTimeout = TimeSpan.FromSeconds(3);

    // 按协议族分别探测出口地址
    private static readonly string[] Ipv4Endpoints =
    [
        "https://ipv4.icanhazip.com",
        "https://api.ipify.org?format=json"
    ];

    private static readonly string[] Ipv6Endpoints =
    [
        "https://ipv6.icanhazip.com",
        "https://api6.ipify.org?format=json"
    ];

    /// <summary>延迟探测节点，请求失败的节点不计入平均</summary>
    private static readonly string[] LatencyNodes =
    [
        "https://www.baidu.com/favicon.ico",
        "https://www.aliyun.com/favicon.ico",
        "https://www.tencent.com/favicon.ico"
    ];

    /// <summary>虚拟网卡</summary>
    private static readonly string[] VirtualAdapterPrefixes =
    [
        "docker", "veth", "br-", "virbr", "vmnet", "vboxnet", "tun", "tap"
    ];

    private readonly SemaphoreSlim _gate = new(1, 1);

    private IpLookup? _cached;

    private DateTimeOffset _fetchedAt = DateTimeOffset.MinValue;

    public async Task<IpInfoResponse> GetAsync(bool force, bool includeDetails, CancellationToken ct)
    {
        if (IsFresh(force)) return ToResponse(_cached!, includeDetails);

        await _gate.WaitAsync(ct);
        try
        {
            if (IsFresh(force)) return ToResponse(_cached!, includeDetails);

            IpLookup lookup;
            try
            {
                lookup = await LookupAsync(ct);
            }
            catch (IpInfoUnavailableException) when (_cached is not null)
            {
                // 第三方接口不可用时使用上一次结果
                _fetchedAt = DateTimeOffset.UtcNow;
                return ToResponse(_cached, includeDetails);
            }

            _cached = lookup;
            _fetchedAt = DateTimeOffset.UtcNow;

            return ToResponse(lookup, includeDetails);
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

    /// <summary>
    /// 默认IPV6
    /// </summary>
    /// <returns></returns>
    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = RequestTimeout;
        client.DefaultRequestHeaders.UserAgent.ParseAdd("atlas-ip-info");
        return client;
    }
    
    /// <summary>
    /// 强制IPV4
    /// </summary>
    /// <returns></returns>
    /// <exception cref="HttpRequestException"></exception>
    private static HttpClient CreateIpv4Client()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = RequestTimeout,
            ConnectCallback = async (context, ct) =>
            {
                IPAddress[] addresses;

                try
                {
                    addresses = await Dns.GetHostAddressesAsync(
                        context.DnsEndPoint.Host, AddressFamily.InterNetwork, ct);
                }
                catch (SocketException e)
                {
                    throw new HttpRequestException("IPv4 解析失败", e);
                }

                Exception? failure = null;

                foreach (var address in addresses)
                {
                    var socket = new Socket(
                        AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true
                    };

                    try
                    {
                        await socket.ConnectAsync(
                            new IPEndPoint(address, context.DnsEndPoint.Port), ct);
                        return new NetworkStream(socket, true);
                    }
                    catch (SocketException e)
                    {
                        failure = e;
                        socket.Dispose();
                    }
                }

                throw new HttpRequestException("无法通过 IPv4 连接", failure);
            }
        };

        var client = new HttpClient(handler) { Timeout = RequestTimeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("atlas-ip-info");
        return client;
    }

    /// <summary>IPv4与IPv6出口各查一次</summary>
    private async Task<PublicIpEntry[]> ResolveIpipEntriesAsync(CancellationToken ct)
    {
        using var ipv4Client = CreateIpv4Client();
        using var client = CreateClient();

        var ipv4Task = TryIpipAsync(ipv4Client, ct);
        var defaultTask = TryIpipAsync(client, ct);
        await Task.WhenAll(ipv4Task, defaultTask);

        return new[] { ipv4Task.Result, defaultTask.Result }
            .OfType<PublicIpEntry>()
            .DistinctBy(entry => entry.Address)
            .ToArray();
    }

    private async Task<IpLookup> LookupAsync(CancellationToken ct)
    {
        using var client = CreateClient();

        // 地址探测、中文归属地、延迟探测并行发出
        var ipv4Task = TryGetAddressAsync(client, Ipv4Endpoints, AddressFamily.InterNetwork, ct);
        var ipv6Task = TryGetAddressAsync(client, Ipv6Endpoints, AddressFamily.InterNetworkV6, ct);
        var ipipTask = ResolveIpipEntriesAsync(ct);
        var latencyTask = ProbeLatencyAsync(ct);

        await Task.WhenAll(ipv4Task, ipv6Task, ipipTask);

        var entries = new List<PublicIpEntry>();
        
        if (ipv4Task.Result is { } ipv4) entries.Add(ipv4);
        if (ipv6Task.Result is { } ipv6) entries.Add(ipv6);

        foreach (var entry in ipipTask.Result)
        {
            // 同地址换成 ipip 的中文归属地
            var index = entries.FindIndex(item => item.Address == entry.Address);
            if (index >= 0) entries[index] = entry;
            else entries.Add(entry);
        }

        if (entries.Count == 0)
        {
            var fallback = await TryIpWhoIsAsync(client, null, ct);
            if (fallback is null)
            {
                logger.LogWarning("公网 IP 查询失败：第三方接口均不可用");
                throw new IpInfoUnavailableException("无法获取公网 IP，请检查服务器外网连接");
            }

            entries.Add(fallback);
        }

        await FillMissingGeoAsync(client, entries, ct);

        var (latencyMs, nodes) = await latencyTask;

        return new IpLookup(entries, latencyMs, nodes);
    }

    /// <summary>ipip 返回中文省市与运营商，只覆盖默认出口地址</summary>
    private async Task<PublicIpEntry?> TryIpipAsync(HttpClient client, CancellationToken ct)
    {
        using var doc = await GetJsonAsync(client, "https://myip.ipip.net/json", ct);
        if (doc is null) return null;

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (ReadString(root, "ret") != "ok") return null;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return null;

        var ip = ReadString(data, "ip");
        if (ip is null) return null;

        string? country = null, region = null, city = null, isp = null;
        if (data.TryGetProperty("location", out var location))
        {
            // ["中国", "贵州", "遵义", "", "联通"]
            country = ArrayPart(location, 0);
            region = ArrayPart(location, 1);
            city = ArrayPart(location, 2);
            isp = ArrayPart(location, 4);
        }

        return new PublicIpEntry(ip, VersionOf(ip), country, region, city, isp, null);
    }

    /// <summary>address 为空时查询本机默认出口的归属地</summary>
    private async Task<PublicIpEntry?> TryIpWhoIsAsync(
        HttpClient client, string? address, CancellationToken ct)
    {
        var url = address is null
            ? "https://ipwho.is/"
            : $"https://ipwho.is/{address}";

        using var doc = await GetJsonAsync(client, url, ct);
        if (doc is null) return null;

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!root.TryGetProperty("success", out var success)
            || success.ValueKind != JsonValueKind.True)
            return null;

        var ip = ReadString(root, "ip") ?? address;
        if (ip is null) return null;

        string? isp = null, asn = null;
        if (root.TryGetProperty("connection", out var connection)
            && connection.ValueKind == JsonValueKind.Object)
        {
            isp = ReadString(connection, "isp") ?? ReadString(connection, "org");
            asn = ReadAsn(connection);
        }

        return new PublicIpEntry(
            ip,
            VersionOf(ip),
            ReadString(root, "country"),
            ReadString(root, "region"),
            ReadString(root, "city"),
            isp,
            asn);
    }

    private async Task<PublicIpEntry?> TryIpInfoAsync(HttpClient client, string? address, CancellationToken ct)
    {
        var url = address is null
            ? "https://ipinfo.io/json"
            : $"https://ipinfo.io/{address}/json";

        using var doc = await GetJsonAsync(client, url, ct);
        if (doc is null) return null;

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;

        var ip = ReadString(root, "ip") ?? address;
        if (ip is null) return null;

        // org 形如 "AS4837 CHINA UNICOM China169 Backbone"
        SplitAsn(ReadString(root, "org"), out var asn, out var isp);

        return new PublicIpEntry(
            ip,
            VersionOf(ip),
            ReadString(root, "country"),
            ReadString(root, "region"),
            ReadString(root, "city"),
            isp,
            asn);
    }

    /// <summary>按顺序尝试同一协议族的探测地址，返回首个合法结果</summary>
    private async Task<PublicIpEntry?> TryGetAddressAsync(
        HttpClient client,
        string[] urls,
        AddressFamily family,
        CancellationToken ct)
    {
        foreach (var url in urls)
        {
            var text = await GetTextAsync(client, url, ct);
            if (text is null) continue;

            var address = ParseAddress(text);
            if (address is null || address.AddressFamily != family) continue;

            return new PublicIpEntry(address.ToString(), family == AddressFamily.InterNetworkV6 ? 6 : 4,
                null, null, null, null, null);
        }

        return null;
    }

    /// <summary>icanhazip 返回纯文本地址，ipify 返回 {"ip":"..."}</summary>
    private static IPAddress? ParseAddress(string body)
    {
        var text = body.Trim();
        if (text.Length == 0 || text.Length > 128) return null;

        if (text.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind != JsonValueKind.Object
                    || !doc.RootElement.TryGetProperty("ip", out var value)
                    || value.ValueKind != JsonValueKind.String)
                    return null;

                text = value.GetString()?.Trim() ?? string.Empty;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        return IPAddress.TryParse(text, out var address) ? address : null;
    }

    private async Task FillMissingGeoAsync(
        HttpClient client, List<PublicIpEntry> entries, CancellationToken ct)
    {
        var missing = entries.Where(entry => entry.Country is null
                                             && entry.Region is null
                                             && entry.City is null
                                             && entry.Isp is null)
            .ToArray();

        if (missing.Length == 0) return;

        var lookups = await Task.WhenAll(
            missing.Select(entry => TryIpWhoIsAsync(client, entry.Address, ct)));

        for (var index = 0; index < missing.Length; index++)
        {
            var filled = lookups[index]
                         ?? await TryIpInfoAsync(client, missing[index].Address, ct);
            if (filled is null) continue;

            var position = entries.IndexOf(missing[index]);
            if (position >= 0) entries[position] = filled;
        }
    }

    private async Task<(int? Ms, int Nodes)> ProbeLatencyAsync(CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient();
        client.Timeout = LatencyTimeout;

        var results = await Task.WhenAll(
            LatencyNodes.Select(url => MeasureAsync(client, url, ct)));

        var successes = results.Where(result => result.HasValue)
            .Select(result => result!.Value)
            .ToArray();

        return successes.Length == 0
            ? (null, 0)
            : ((int)Math.Round(successes.Average()), successes.Length);
    }

    private static async Task<double?> MeasureAsync(HttpClient client, string url, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await client.GetAsync(
                url, HttpCompletionOption.ResponseHeadersRead, ct);
            stopwatch.Stop();

            return response.IsSuccessStatusCode ? stopwatch.Elapsed.TotalMilliseconds : null;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<JsonDocument?> GetJsonAsync(
        HttpClient client, string url, CancellationToken ct)
    {
        var body = await GetTextAsync(client, url, ct);
        if (body is null) return null;

        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException e)
        {
            logger.LogDebug(e, "IP 信息接口返回内容无法解析：{Url}", url);
            return null;
        }
    }

    private async Task<string?> GetTextAsync(HttpClient client, string url, CancellationToken ct)
    {
        try
        {
            using var response = await client.GetAsync(
                url, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode) return null;
            if (response.Content.Headers.ContentLength > MaxResponseBytes) return null;

            var body = await response.Content.ReadAsStringAsync(ct);
            if (body.Length > MaxResponseBytes) return null;

            return body;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogDebug("IP 信息接口超时：{Url}", url);
            return null;
        }
        catch (HttpRequestException e)
        {
            logger.LogDebug(e, "IP 信息接口请求失败：{Url}", url);
            return null;
        }
    }

    private static IpInfoResponse ToResponse(IpLookup lookup, bool includeDetails)
    {
        var entries = includeDetails
            ? lookup.Entries.ToArray()
            : lookup.Entries
                .Select(entry => entry with
                {
                    Address = MaskAddress(entry.Address),
                    // 匿名访客不显示运营商和 ASN
                    Isp = null,
                    Asn = null
                })
                .ToArray();

        return new IpInfoResponse(
            entries,
            // 匿名访客不显示内网地址
            includeDetails ? ResolveLocalIp() : null,
            lookup.LatencyMs ?? 0,
            lookup.LatencyNodes,
            !includeDetails);
    }

    /// <summary>IPv4 保留前两段，IPv6 保留前两组，用以区分协议与大致运营商</summary>
    private static string MaskAddress(string address)
    {
        if (!IPAddress.TryParse(address, out var parsed)) return "***";

        if (parsed.AddressFamily == AddressFamily.InterNetwork)
        {
            var parts = address.Split('.');
            return parts.Length == 4 ? $"{parts[0]}.{parts[1]}.*.*" : "*.*.*.*";
        }

        if (parsed.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var groups = address.Split(':');
            return groups.Length >= 2 && groups[0].Length > 0
                ? $"{groups[0]}:{groups[1]}:***"
                : "***";
        }

        return "***";
    }

    private static string? ResolveLocalIp()
    {
        string? virtualAddress = null;

        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up) continue;
            if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(unicast.Address)) continue;

                var address = unicast.Address.ToString();

                if (IsVirtualAdapter(adapter.Name))
                {
                    virtualAddress ??= address;
                    continue;
                }

                return address;
            }
        }

        return virtualAddress;
    }

    private static bool IsVirtualAdapter(string name)
    {
        return VirtualAdapterPrefixes.Any(prefix =>
            name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static int VersionOf(string address)
    {
        return IPAddress.TryParse(address, out var parsed)
               && parsed.AddressFamily == AddressFamily.InterNetworkV6
            ? 6
            : 4;
    }

    /// <summary>ipwho 的 asn 是数字（4837），ipinfo 的 org 里是字符串（AS4837）</summary>
    private static string? ReadAsn(JsonElement element)
    {
        if (!element.TryGetProperty("asn", out var value)) return null;

        var raw = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(raw)) return null;

        var text = raw.Trim();
        return text.StartsWith("AS", StringComparison.OrdinalIgnoreCase) ? text : $"AS{text}";
    }

    private static string? ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? NullIfBlank(value.GetString()) : null;
    }

    private static string? ArrayPart(JsonElement array, int index)
    {
        if (array.ValueKind != JsonValueKind.Array) return null;
        if (array.GetArrayLength() <= index) return null;

        var value = array[index];
        return value.ValueKind == JsonValueKind.String ? NullIfBlank(value.GetString()) : null;
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void SplitAsn(string? org, out string? asn, out string? isp)
    {
        asn = null;
        isp = null;

        if (string.IsNullOrWhiteSpace(org)) return;

        var parts = org.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length > 0 && parts[0].StartsWith("AS", StringComparison.OrdinalIgnoreCase))
        {
            asn = parts[0];
            if (parts.Length > 1) isp = parts[1];
            return;
        }

        isp = org.Trim();
    }

    private sealed record IpLookup(
        List<PublicIpEntry> Entries,
        int? LatencyMs,
        int LatencyNodes);
}
