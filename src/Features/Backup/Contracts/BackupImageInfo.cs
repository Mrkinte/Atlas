namespace Atlas.Features.Backup.Contracts;

/// <summary>
///     图库中一张图片的元数据，实际文件位于 ZIP 内相同的相对路径下
/// </summary>
public sealed record BackupImageInfo(
    string RelativePath,
    string OriginalName,
    string Kind,
    DateTime CreatedAt);