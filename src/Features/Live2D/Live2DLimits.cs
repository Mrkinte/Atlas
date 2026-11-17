namespace Atlas.Features.Live2D;

/// <summary>
///     Live2D 模型上传与解压的大小上限，备份包与 Program.cs 的请求体放宽都引用这里
/// </summary>
public static class Live2DLimits
{
    /// <summary>100 MB</summary>
    public const long MaxArchiveSize = 100L * 1024 * 1024;

    /// <summary>解压后总字节上限，防压缩炸弹，与界面提示一致</summary>
    public const long MaxExtractedTotalBytes = 200L * 1024 * 1024;

    /// <summary>上传端点的 Kestrel 请求体上限（128 MB），为 multipart 编码开销留余量</summary>
    public const long ModelUploadRequestBodyBytes = 128L * 1024 * 1024;
}