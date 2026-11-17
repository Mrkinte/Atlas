namespace Atlas.Features.SystemStatus.Contracts;

/// <summary>单台硬件的系统状态快照。匿名连接不会拿到硬件 ID 与真实名称</summary>
public sealed record SystemStatusDevice(
    string HardwareId,
    string HardwareName,
    double? CpuUsagePercent,
    long? MemoryTotalBytes,
    long? MemoryUsedBytes,
    long ProcessWorkingSetBytes,
    long ManagedHeapBytes,
    long? DiskTotalBytes,
    long? DiskFreeBytes,
    double? LoadAverage1m,
    long? UptimeSeconds);

/// <summary>
///     WebSocket 推送消息。Type 为 status 时 Devices 是当前连接可见的硬件；
///     Type 为 denied 表示硬件不在公开白名单中，未登录连接不会收到任何指标
/// </summary>
public sealed record SystemStatusMessage(
    string Type,
    DateTimeOffset Timestamp,
    SystemStatusDevice[] Devices,
    string? Reason);

/// <summary>当前硬件标识，供设置页展示并配置公开白名单</summary>
public sealed record SystemStatusHardware(string Id, string Name);