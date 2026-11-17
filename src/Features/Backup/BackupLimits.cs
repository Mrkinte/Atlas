using Atlas.Features.Live2D;

namespace Atlas.Features.Backup;

/// <summary>
///     备份包与恢复流程的大小上限。Program.cs 放宽恢复端点的请求体时也引用这里
/// </summary>
public static class BackupLimits
{
    /// <summary>当前备份ZIP包版本</summary>
    public const int CurrentVersion = 1;

    /// <summary>支持的最小备份ZIP包版本</summary>
    public const int MinimumSupportedVersion = 1;

    /// <summary>512 MB</summary>
    public const long MaxRestoreArchiveBytes = 512L * 1024 * 1024;

    /// <summary>50 MB</summary>
    public const long MaxEntryBytes = 50L * 1024 * 1024;

    /// <summary>单个模型文件不会超过完整模型的解压上限，直接复用该常量</summary>
    public const long MaxLive2DEntryBytes = Live2DLimits.MaxExtractedTotalBytes;

    /// <summary>1 GB</summary>
    public const long MaxTotalUncompressedBytes = 1024L * 1024 * 1024;
}