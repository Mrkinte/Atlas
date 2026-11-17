using System.Collections.Concurrent;

namespace Atlas.Features.Auth;

/// <summary>
///     按用户名限制登录失败次数，补 IP 限流覆盖不到的单账号暴力破解
/// </summary>
public sealed class LoginAttemptLimiter
{
    private const int MaxFailures = 5;

    // 条目过多时清理过期记录，避免随机用户名撑大内存
    private const int CleanupThreshold = 512;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, Entry> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsBlocked(string username, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;

        if (!_entries.TryGetValue(username, out var entry)) return false;

        lock (entry)
        {
            if (entry.BlockedUntil is not { } blockedUntil) return false;

            var now = DateTimeOffset.UtcNow;

            if (blockedUntil > now)
            {
                retryAfter = blockedUntil - now;
                return true;
            }

            // 锁定期结束，重新计数
            entry.BlockedUntil = null;
            entry.Failures = 0;
            entry.WindowStart = now;
            return false;
        }
    }

    public void RegisterFailure(string username)
    {
        if (_entries.Count > CleanupThreshold) CleanupStale();

        var entry = _entries.GetOrAdd(username, _ => new Entry());

        lock (entry)
        {
            var now = DateTimeOffset.UtcNow;

            if (now - entry.WindowStart > Window)
            {
                entry.WindowStart = now;
                entry.Failures = 0;
            }

            entry.Failures++;

            if (entry.Failures >= MaxFailures) entry.BlockedUntil = now + Lockout;
        }
    }

    public void Reset(string username)
    {
        _entries.TryRemove(username, out _);
    }

    private void CleanupStale()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var (username, entry) in _entries)
        {
            bool stale;

            lock (entry)
            {
                stale = entry.BlockedUntil is null && now - entry.WindowStart > Window;
            }

            if (stale) _entries.TryRemove(username, out _);
        }
    }

    private sealed class Entry
    {
        public DateTimeOffset? BlockedUntil;
        public int Failures;

        public DateTimeOffset WindowStart = DateTimeOffset.UtcNow;
    }
}