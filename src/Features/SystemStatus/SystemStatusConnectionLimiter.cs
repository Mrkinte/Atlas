namespace Atlas.Features.SystemStatus;

/// <summary>
///     WebSocket 连接数限制，防止公开端点被大量连接占用。按客户端 IP 与全局总量分别计算，
///     连接关闭后自动释放
/// </summary>
public sealed class SystemStatusConnectionLimiter
{
    private const int MaxConnectionsPerIp = 6;
    private const int MaxConnectionsTotal = 48;
    private readonly Dictionary<string, int> _perIp = new(StringComparer.Ordinal);

    private readonly object _sync = new();

    private int _total;

    public IDisposable? TryAcquire(string clientKey)
    {
        lock (_sync)
        {
            if (_total >= MaxConnectionsTotal) return null;

            var current = _perIp.GetValueOrDefault(clientKey);
            if (current >= MaxConnectionsPerIp) return null;

            _perIp[clientKey] = current + 1;
            _total++;
            return new Lease(this, clientKey);
        }
    }

    private void Release(string clientKey)
    {
        lock (_sync)
        {
            var current = _perIp.GetValueOrDefault(clientKey);
            if (current <= 1)
                _perIp.Remove(clientKey);
            else
                _perIp[clientKey] = current - 1;

            if (_total > 0) _total--;
        }
    }

    private sealed class Lease(
        SystemStatusConnectionLimiter limiter,
        string clientKey) : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (_released) return;
            _released = true;
            limiter.Release(clientKey);
        }
    }
}