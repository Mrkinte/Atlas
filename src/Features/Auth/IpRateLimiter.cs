using System.Globalization;
using System.Net;

namespace Atlas.Features.Auth;

/// <summary>按客户端 IP 限流的策略名，供端点元数据引用</summary>
public static class IpRateLimitPolicies
{
    public const string Auth = "auth";

    public const string Refresh = "refresh";
}

public sealed record IpRateLimitMetadata(string Policy);

public static class IpRateLimitExtensions
{
    /// <summary>给端点挂上限流策略；未声明策略的端点不受限</summary>
    public static TBuilder RequireIpRateLimit<TBuilder>(this TBuilder builder, string policy)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.WithMetadata(new IpRateLimitMetadata(policy));
    }

    /// <summary>必须位于 UseRouting 之后，策略来自端点元数据</summary>
    public static IApplicationBuilder UseIpRateLimiting(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var metadata = context.GetEndpoint()?.Metadata.GetMetadata<IpRateLimitMetadata>();
            if (metadata is null)
            {
                await next(context);
                return;
            }

            var limiter = context.RequestServices.GetRequiredService<IpRateLimiter>();
            if (!limiter.TryAcquire(
                    metadata.Policy, context.Connection.RemoteIpAddress, out var retryAfter))
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter = Math
                    .Ceiling(retryAfter.TotalSeconds)
                    .ToString(CultureInfo.InvariantCulture);
                return;
            }

            await next(context);
        });
    }
}

/// <summary>
///     按客户端 IP 的固定窗口限流。不用框架的 RateLimitPartition：它按分区缓存 limiter
///     且不淘汰，IP 越多（IPv6 隐私地址、扫描流量）内存越涨。这里窗口过期即清理，
///     条目数只与最近一个窗口内出现过的 IP 数相关
/// </summary>
public sealed class IpRateLimiter
{
    // 清理检查的最小间隔，避免每个请求都遍历全部条目
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);

    private static readonly Dictionary<string, Definition> Definitions =
        new(StringComparer.Ordinal)
        {
            [IpRateLimitPolicies.Auth] = new Definition(10, TimeSpan.FromMinutes(1)),
            [IpRateLimitPolicies.Refresh] = new Definition(30, TimeSpan.FromMinutes(1))
        };

    private readonly object _sync = new();

    private readonly Dictionary<string, Dictionary<IPAddress, Window>> _windows =
        new(StringComparer.Ordinal);

    private DateTimeOffset _lastCleanup = DateTimeOffset.MinValue;

    /// <summary>未声明的策略直接放行，避免配置遗漏变成 500</summary>
    public bool TryAcquire(string policy, IPAddress? address, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;

        if (!Definitions.TryGetValue(policy, out var definition)) return true;

        var client = address ?? IPAddress.None;
        var now = DateTimeOffset.UtcNow;

        lock (_sync)
        {
            CleanupIfNeeded(now);

            if (!_windows.TryGetValue(policy, out var entries))
            {
                entries = new Dictionary<IPAddress, Window>();
                _windows[policy] = entries;
            }

            if (!entries.TryGetValue(client, out var window) || now >= window.ResetAt)
            {
                window = new Window { ResetAt = now + definition.Window };
                entries[client] = window;
            }

            if (window.Count >= definition.PermitLimit)
            {
                retryAfter = window.ResetAt - now;
                return false;
            }

            window.Count++;
            return true;
        }
    }

    private void CleanupIfNeeded(DateTimeOffset now)
    {
        if (now - _lastCleanup < CleanupInterval) return;

        _lastCleanup = now;

        foreach (var entries in _windows.Values)
        {
            // 遍历中不能删，先收集过期 key 再删
            List<IPAddress>? stale = null;

            foreach (var (address, window) in entries)
            {
                if (now < window.ResetAt) continue;

                stale ??= [];
                stale.Add(address);
            }

            if (stale is null) continue;

            foreach (var address in stale) entries.Remove(address);
        }
    }

    private sealed class Window
    {
        public int Count;

        public DateTimeOffset ResetAt;
    }

    private readonly record struct Definition(int PermitLimit, TimeSpan Window);
}