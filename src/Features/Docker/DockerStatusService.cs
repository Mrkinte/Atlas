using System.IO.Pipes;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Atlas.Features.Docker.Contracts;
using Microsoft.Extensions.Configuration;

namespace Atlas.Features.Docker;

/// <summary>
///     读取 Docker Engine 的容器、镜像与资源用量。匿名请求只暴露聚合值，
///     容器详情由端点按登录态裁剪
/// </summary>
public sealed class DockerStatusService : IDisposable
{
    private const string DefaultUnixSocket = "unix:///var/run/docker.sock";

    private const string DefaultWindowsPipe = "npipe:////./pipe/docker_engine";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    private readonly string _defaultEndpoint;

    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly ILogger<DockerStatusService> _logger;

    private volatile CacheEntry? _cache;

    private HttpClient? _client;

    private string? _clientEndpoint;

    private string? _warnedEndpoint;

    public DockerStatusService(
        IConfiguration configuration,
        ILogger<DockerStatusService> logger)
    {
        _logger = logger;

        var configured = configuration["Docker:Endpoint"];
        if (string.IsNullOrWhiteSpace(configured))
            configured = Environment.GetEnvironmentVariable("DOCKER_HOST");

        _defaultEndpoint = !string.IsNullOrWhiteSpace(configured)
            ? configured.Trim()
            : (OperatingSystem.IsWindows() ? DefaultWindowsPipe : DefaultUnixSocket);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _gate.Dispose();
    }

    internal async Task<DockerSnapshot> GetAsync(string? endpoint, CancellationToken ct)
    {
        var resolved = string.IsNullOrWhiteSpace(endpoint)
            ? _defaultEndpoint
            : endpoint.Trim();

        if (TryReadCache(resolved, out var cached)) return cached;

        await _gate.WaitAsync(ct);
        try
        {
            if (TryReadCache(resolved, out cached)) return cached;

            var client = EnsureClient(resolved);
            DockerSnapshot snapshot;

            if (client is null)
            {
                snapshot = DockerSnapshot.Unavailable(DateTimeOffset.UtcNow);
            }
            else
            {
                try
                {
                    snapshot = await FetchAsync(client, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e) when (
                    e is HttpRequestException or IOException or JsonException
                        or TaskCanceledException)
                {
                    _logger.LogDebug(e, "读取 Docker 状态失败");
                    snapshot = DockerSnapshot.Unavailable(DateTimeOffset.UtcNow);
                }
            }

            _cache = new CacheEntry(resolved, snapshot, DateTimeOffset.UtcNow);
            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal static DockerSnapshot CreateDemoSnapshot()
    {
        var containers = new[]
        {
            new DockerContainerInfo(
                "6f1f3c2a9d10", "nginx", "nginx:alpine", "running", "Up 3 days"),
            new DockerContainerInfo(
                "1b9d4e6c8a22", "postgres", "postgres:16", "running", "Up 3 days (healthy)"),
            new DockerContainerInfo(
                "9c3a7b1e5f44", "redis", "redis:7-alpine", "running", "Up 3 days"),
            new DockerContainerInfo(
                "4d8e2f6a1b33", "watchtower", "containrrr/watchtower", "exited",
                "Exited (0) 2 days ago")
        };

        return new DockerSnapshot(
            true, containers.Length, 3, 0, 1, 12, 9.9, 1_936_273_408,
            DateTimeOffset.UtcNow, containers);
    }

    internal static DockerConnection? ParseConnection(string endpoint)
    {
        var value = endpoint.Trim();
        if (value.Length == 0) return null;

        if (value.StartsWith("unix://", StringComparison.OrdinalIgnoreCase))
        {
            var path = value["unix://".Length..];
            return path.Length == 0
                ? null
                : new DockerConnection(new Uri("http://docker/"), path, null);
        }

        if (IsNamedPipe(value))
        {
            var name = ExtractPipeName(value);
            return name.Length == 0
                ? null
                : new DockerConnection(new Uri("http://docker/"), null, name);
        }

        if (value.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase))
            value = $"http://{value["tcp://".Length..]}";

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https")
            return new DockerConnection(uri, null, null);

        if (value.StartsWith('/'))
            return new DockerConnection(new Uri("http://docker/"), value, null);

        return null;
    }

    private bool TryReadCache(string endpoint, out DockerSnapshot snapshot)
    {
        if (_cache is { } entry
            && string.Equals(entry.Endpoint, endpoint, StringComparison.Ordinal)
            && DateTimeOffset.UtcNow - entry.At < CacheDuration)
        {
            snapshot = entry.Snapshot;
            return true;
        }

        snapshot = null!;
        return false;
    }

    private HttpClient? EnsureClient(string endpoint)
    {
        if (_client is not null
            && string.Equals(_clientEndpoint, endpoint, StringComparison.Ordinal))
            return _client;

        _client?.Dispose();
        _client = null;
        _clientEndpoint = endpoint;

        var connection = ParseConnection(endpoint);
        if (connection is null)
        {
            if (!string.Equals(_warnedEndpoint, endpoint, StringComparison.Ordinal))
            {
                _logger.LogWarning("无法解析 Docker 端点：{Endpoint}", endpoint);
                _warnedEndpoint = endpoint;
            }

            return null;
        }

        var handler = new SocketsHttpHandler { ConnectTimeout = RequestTimeout };

        if (connection.UnixSocketPath is { } unixPath)
        {
            handler.ConnectCallback = async (_, ct) =>
            {
                var socket = new Socket(
                    AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

                try
                {
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(unixPath), ct);
                    return new NetworkStream(socket, true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            };
        }
        else if (connection.PipeName is { } pipeName)
        {
            handler.ConnectCallback = async (_, ct) =>
            {
                var pipe = new NamedPipeClientStream(
                    ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

                try
                {
                    await pipe.ConnectAsync(ct);
                    return pipe;
                }
                catch
                {
                    pipe.Dispose();
                    throw;
                }
            };
        }

        _client = new HttpClient(handler)
        {
            BaseAddress = connection.BaseAddress,
            Timeout = RequestTimeout
        };

        return _client;
    }

    private static async Task<DockerSnapshot> FetchAsync(
        HttpClient client, CancellationToken ct)
    {
        var containersTask = GetJsonAsync(
            client, "/containers/json?all=1",
            DockerJsonContext.Default.DockerApiContainerArray, ct);
        var infoTask = TryGetJsonAsync(
            client, "/info", DockerJsonContext.Default.DockerApiInfo, ct);

        await Task.WhenAll(containersTask, infoTask);

        var containers = await containersTask;
        var info = await infoTask;
        var stats = await FetchStatsAsync(client, containers, ct);

        return DockerSnapshot.FromApi(
            containers, info?.Images ?? 0, stats, DateTimeOffset.UtcNow);
    }

    private static async Task<DockerApiStats[]> FetchStatsAsync(
        HttpClient client, DockerApiContainer[] containers, CancellationToken ct)
    {
        var tasks = containers
            .Where(container => container.State == "running"
                                && !string.IsNullOrEmpty(container.Id))
            .Select(container => TryGetStatsAsync(client, container.Id!, ct))
            .ToArray();

        if (tasks.Length == 0) return [];

        return (await Task.WhenAll(tasks)).OfType<DockerApiStats>().ToArray();
    }

    private static Task<DockerApiStats?> TryGetStatsAsync(
        HttpClient client, string id, CancellationToken ct)
    {
        return TryGetJsonAsync(
            client,
            $"/containers/{Uri.EscapeDataString(id)}/stats?stream=false",
            DockerJsonContext.Default.DockerApiStats,
            ct);
    }

    private static async Task<T> GetJsonAsync<T>(
        HttpClient client,
        string path,
        JsonTypeInfo<T> typeInfo,
        CancellationToken ct) where T : class
    {
        using var response = await client.GetAsync(
            path, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync(stream, typeInfo, ct)
               ?? throw new JsonException("Docker 返回空响应");
    }

    private static async Task<T?> TryGetJsonAsync<T>(
        HttpClient client,
        string path,
        JsonTypeInfo<T> typeInfo,
        CancellationToken ct) where T : class
    {
        try
        {
            return await GetJsonAsync(client, path, typeInfo, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (
            e is HttpRequestException or IOException or JsonException
                or TaskCanceledException)
        {
            return null;
        }
    }

    private static bool IsNamedPipe(string value)
    {
        return value.StartsWith("npipe://", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("//./pipe/", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith(@"\\.\pipe\", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith(@".\pipe\", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractPipeName(string value)
    {
        var path = value.StartsWith("npipe://", StringComparison.OrdinalIgnoreCase)
            ? value["npipe://".Length..]
            : value;

        path = path.Replace('/', '\\');

        const string marker = @"\pipe\";
        var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        return (index >= 0 ? path[(index + marker.Length)..] : path.Trim('\\')).Trim();
    }

    private sealed record CacheEntry(
        string Endpoint, DockerSnapshot Snapshot, DateTimeOffset At);
}

internal sealed record DockerConnection(
    Uri BaseAddress, string? UnixSocketPath, string? PipeName);

internal sealed record DockerSnapshot(
    bool Available,
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
    public static DockerSnapshot Unavailable(DateTimeOffset sampledAt)
    {
        return new DockerSnapshot(
            false, 0, 0, 0, 0, 0, null, null, sampledAt, []);
    }

    public static DockerSnapshot FromApi(
        DockerApiContainer[] containers,
        int images,
        DockerApiStats[] stats,
        DateTimeOffset sampledAt)
    {
        var items = new DockerContainerInfo[containers.Length];
        var running = 0;
        var paused = 0;

        for (var index = 0; index < containers.Length; index++)
        {
            var container = containers[index];
            var state = container.State ?? string.Empty;

            if (state == "running") running++;
            else if (state == "paused") paused++;

            var id = container.Id ?? string.Empty;

            items[index] = new DockerContainerInfo(
                id.Length > 12 ? id[..12] : id,
                NormalizeName(container.Names),
                container.Image ?? string.Empty,
                state,
                container.Status ?? string.Empty);
        }

        // 没有运行中容器时聚合值就是 0；有运行容器但 stats 拉取失败时保持 null 显示为未知
        var (cpuPercent, memoryBytes) = running == 0
            ? (0d, 0L)
            : DockerStatsSummary.From(stats);

        return new DockerSnapshot(
            true,
            items.Length,
            running,
            paused,
            items.Length - running - paused,
            images,
            cpuPercent,
            memoryBytes,
            sampledAt,
            items);
    }

    public DockerStatusResponse ToResponse(bool includeDetails)
    {
        return new DockerStatusResponse(
            true,
            Available,
            includeDetails,
            Total,
            Running,
            Paused,
            Stopped,
            Images,
            CpuPercent,
            MemoryBytes,
            SampledAt,
            includeDetails ? Containers : []);
    }

    private static string NormalizeName(string[]? names)
    {
        var name = names is { Length: > 0 } ? names[0] : string.Empty;
        return name.TrimStart('/');
    }
}

internal static class DockerStatsSummary
{
    public static (double? CpuPercent, long? MemoryBytes) From(
        DockerApiStats[] stats)
    {
        var cpuTotal = 0.0;
        var cpuSeen = false;
        var memoryTotal = 0L;
        var memorySeen = false;

        foreach (var item in stats)
        {
            if (CpuPercent(item) is { } cpu)
            {
                cpuTotal += cpu;
                cpuSeen = true;
            }

            if (MemoryBytes(item) is { } memory)
            {
                memoryTotal += memory;
                memorySeen = true;
            }
        }

        return (
            cpuSeen ? Math.Round(cpuTotal, 1) : null,
            memorySeen ? memoryTotal : null);
    }

    private static double? CpuPercent(DockerApiStats stats)
    {
        var cpu = stats.CpuStats;
        var previous = stats.PreCpuStats;
        if (cpu is null || previous is null) return null;

        var total = cpu.CpuUsage?.TotalUsage;
        var previousTotal = previous.CpuUsage?.TotalUsage;
        var system = cpu.SystemCpuUsage;
        var previousSystem = previous.SystemCpuUsage;

        if (total is null || previousTotal is null
            || system is null || previousSystem is null)
            return null;

        var totalDelta = (double)(total.Value - previousTotal.Value);
        var systemDelta = (double)(system.Value - previousSystem.Value);
        if (totalDelta <= 0 || systemDelta <= 0) return null;

        var onlineCpus = cpu.OnlineCpus ?? Environment.ProcessorCount;
        return totalDelta / systemDelta * onlineCpus * 100;
    }

    private static long? MemoryBytes(DockerApiStats stats)
    {
        if (stats.MemoryStats?.Usage is not { } usage) return null;

        var inactive = stats.MemoryStats.Stats?.TotalInactiveFile ?? 0;
        return Math.Max(0, usage - inactive);
    }
}

internal sealed record DockerApiContainer
{
    [JsonPropertyName("Id")] public string? Id { get; init; }

    [JsonPropertyName("Names")] public string[]? Names { get; init; }

    [JsonPropertyName("Image")] public string? Image { get; init; }

    [JsonPropertyName("State")] public string? State { get; init; }

    [JsonPropertyName("Status")] public string? Status { get; init; }
}

internal sealed record DockerApiInfo
{
    [JsonPropertyName("Images")] public int? Images { get; init; }
}

internal sealed record DockerApiStats
{
    [JsonPropertyName("cpu_stats")] public DockerCpuStats? CpuStats { get; init; }

    [JsonPropertyName("precpu_stats")]
    public DockerCpuStats? PreCpuStats { get; init; }

    [JsonPropertyName("memory_stats")]
    public DockerMemoryStats? MemoryStats { get; init; }
}

internal sealed record DockerCpuStats
{
    [JsonPropertyName("cpu_usage")] public DockerCpuUsage? CpuUsage { get; init; }

    [JsonPropertyName("system_cpu_usage")]
    public long? SystemCpuUsage { get; init; }

    [JsonPropertyName("online_cpus")] public int? OnlineCpus { get; init; }
}

internal sealed record DockerCpuUsage
{
    [JsonPropertyName("total_usage")] public long? TotalUsage { get; init; }
}

internal sealed record DockerMemoryStats
{
    [JsonPropertyName("usage")] public long? Usage { get; init; }

    [JsonPropertyName("stats")] public DockerMemoryDetail? Stats { get; init; }
}

internal sealed record DockerMemoryDetail
{
    [JsonPropertyName("total_inactive_file")]
    public long? TotalInactiveFile { get; init; }
}
