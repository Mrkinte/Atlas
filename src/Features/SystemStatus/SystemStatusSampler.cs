using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Atlas.Features.SystemStatus.Contracts;

namespace Atlas.Features.SystemStatus;

/// <summary>
///     采集本机系统状态。Linux 下读取 /proc，其他平台或读取失败时退回进程级指标
///     结果缓存 1 秒，避免多个 WebSocket 连接同时触发重复读取
/// </summary>
public sealed class SystemStatusSampler
{
    private const string ProcStatPath = "/proc/stat";
    private const string ProcMemInfoPath = "/proc/meminfo";
    private const string ProcLoadAvgPath = "/proc/loadavg";
    private const string ProcUptimePath = "/proc/uptime";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(1);

    private readonly object _sync = new();

    private SystemStatusDevice? _cached;
    private DateTime _cachedAt = DateTime.MinValue;
    private long _lastCpuIdle = -1;

    private long _lastCpuTotal = -1;
    private DateTime _lastProcessSampleAt = DateTime.MinValue;
    private long _lastProcessTicks = -1;

    public string HardwareId { get; } =
        string.IsNullOrWhiteSpace(Environment.MachineName)
            ? "local"
            : Environment.MachineName;

    public string HardwareName => HardwareId;

    public SystemStatusDevice Sample()
    {
        lock (_sync)
        {
            var now = DateTime.UtcNow;
            if (_cached is not null && now - _cachedAt < CacheDuration) return _cached;

            var memory = ReadMemory();
            var disk = ReadDisk();
            var workingSet = ReadWorkingSet();
            var managedHeap = GC.GetTotalMemory(false);

            var snapshot = new SystemStatusDevice(
                HardwareId,
                HardwareName,
                ReadCpuUsage(),
                memory.Total,
                memory.Used,
                workingSet,
                managedHeap,
                disk.Total,
                disk.Free,
                ReadLoadAverage(),
                ReadUptimeSeconds());

            _cached = snapshot;
            _cachedAt = now;
            return snapshot;
        }
    }

    private double? ReadCpuUsage()
    {
        return ReadLinuxCpuUsage() ?? ReadProcessCpuUsage();
    }

    /// <summary>读 /proc/stat 的总 CPU 时间差，得到系统整体使用率</summary>
    private double? ReadLinuxCpuUsage()
    {
        try
        {
            if (!File.Exists(ProcStatPath)) return null;

            var line = File.ReadLines(ProcStatPath)
                .FirstOrDefault(it => it.StartsWith("cpu ", StringComparison.Ordinal));
            if (line is null) return null;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 9) return null;

            var total = 0L;
            var idle = 0L;

            for (var i = 1; i <= 8; i++)
            {
                var value = long.Parse(parts[i], CultureInfo.InvariantCulture);
                total += value;
                if (i is 4 or 5) idle += value;
            }

            if (_lastCpuTotal < 0)
            {
                _lastCpuTotal = total;
                _lastCpuIdle = idle;
                return null;
            }

            var deltaTotal = total - _lastCpuTotal;
            var deltaIdle = idle - _lastCpuIdle;
            _lastCpuTotal = total;
            _lastCpuIdle = idle;

            if (deltaTotal <= 0) return null;

            return Math.Clamp(
                (1 - (double)deltaIdle / deltaTotal) * 100, 0, 100);
        }
        catch (Exception e) when (
            e is IOException or UnauthorizedAccessException or FormatException)
        {
            return null;
        }
    }

    /// <summary>非 Linux 或 /proc 不可读时用进程 CPU 兜底</summary>
    private double? ReadProcessCpuUsage()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var now = DateTime.UtcNow;
            var ticks = process.TotalProcessorTime.Ticks;

            if (_lastProcessTicks < 0)
            {
                _lastProcessTicks = ticks;
                _lastProcessSampleAt = now;
                return null;
            }

            var elapsed = now - _lastProcessSampleAt;
            if (elapsed <= TimeSpan.Zero) return null;

            var delta = ticks - _lastProcessTicks;
            _lastProcessTicks = ticks;
            _lastProcessSampleAt = now;

            var usage = delta / (elapsed.TotalMilliseconds * 10000.0)
                              / Environment.ProcessorCount * 100;

            return Math.Clamp(usage, 0, 100);
        }
        catch (Exception e) when (
            e is InvalidOperationException or NotSupportedException
                or Win32Exception)
        {
            return null;
        }
    }

    private static (long? Total, long? Used) ReadMemory()
    {
        try
        {
            if (!File.Exists(ProcMemInfoPath)) return (null, null);

            long? totalKb = null;
            long? availableKb = null;

            foreach (var line in File.ReadLines(ProcMemInfoPath))
            {
                if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                    totalKb = ParseMemInfoValue(line);
                else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                    availableKb = ParseMemInfoValue(line);

                if (totalKb is not null && availableKb is not null) break;
            }

            if (totalKb is null || availableKb is null) return (null, null);

            var total = totalKb.Value * 1024;
            var available = availableKb.Value * 1024;
            return (total, Math.Max(0, total - available));
        }
        catch (Exception e) when (
            e is IOException or UnauthorizedAccessException or FormatException)
        {
            return (null, null);
        }
    }

    private static long? ParseMemInfoValue(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? long.Parse(parts[1], CultureInfo.InvariantCulture)
            : null;
    }

    private static (long? Total, long? Free) ReadDisk()
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(AppContext.BaseDirectory));
            if (string.IsNullOrEmpty(root)) return (null, null);

            var drive = new DriveInfo(root);
            return drive.IsReady
                ? (drive.TotalSize, drive.AvailableFreeSpace)
                : (null, null);
        }
        catch (Exception e) when (
            e is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return (null, null);
        }
    }

    /// <summary>用 Environment 直接读，比每次 new Process 少一个对象与句柄</summary>
    private static long ReadWorkingSet()
    {
        try
        {
            return Environment.WorkingSet;
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException)
        {
            return 0;
        }
    }

    private static double? ReadLoadAverage()
    {
        try
        {
            if (!File.Exists(ProcLoadAvgPath)) return null;

            var text = File.ReadAllText(ProcLoadAvgPath);
            var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0
                ? double.Parse(parts[0], CultureInfo.InvariantCulture)
                : null;
        }
        catch (Exception e) when (
            e is IOException or UnauthorizedAccessException or FormatException)
        {
            return null;
        }
    }

    private static long? ReadUptimeSeconds()
    {
        try
        {
            if (File.Exists(ProcUptimePath))
            {
                var text = File.ReadAllText(ProcUptimePath);
                var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0) return (long)double.Parse(parts[0], CultureInfo.InvariantCulture);
            }
        }
        catch (Exception e) when (
            e is IOException or UnauthorizedAccessException or FormatException)
        {
            // 退回进程运行时长
        }

        try
        {
            using var process = Process.GetCurrentProcess();
            return (long)(DateTime.UtcNow - process.StartTime.ToUniversalTime())
                .TotalSeconds;
        }
        catch (Exception e) when (
            e is InvalidOperationException or NotSupportedException
                or Win32Exception)
        {
            return null;
        }
    }
}