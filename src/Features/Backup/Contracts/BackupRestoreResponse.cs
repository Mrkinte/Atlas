namespace Atlas.Features.Backup.Contracts;

/// <summary>恢复结果：各段写入数量与上传文件数</summary>
public sealed record BackupRestoreResponse(
    int Cards,
    int Images,
    int Files,
    int Live2DConfigs,
    bool Settings);