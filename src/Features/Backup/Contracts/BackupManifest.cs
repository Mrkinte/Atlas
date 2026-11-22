namespace Atlas.Features.Backup.Contracts;

/// <summary>
///     备份清单，随 ZIP 一起导出。只允许新增字段；删除、改名或改语义时必须
///     提升 <see cref="BackupFormat.CurrentVersion" /> 并提供迁移
/// </summary>
public class BackupManifest
{
    /// <summary>备份格式版本，决定兼容性，与程序版本无关</summary>
    public int Version { get; set; }

    /// <summary>生成备份的程序版本，仅供排障，不参与兼容判断</summary>
    public string AppVersion { get; set; } = string.Empty;

    public DateTime ExportedAt { get; set; }

    /// <summary>本次备份包含的分段；未选中的分段数据为 null，不以空数组代替</summary>
    public List<string>? Sections { get; set; }

    public BackupSettings? Settings { get; set; }

    public BackupLive2D? Live2D { get; set; }

    public List<BackupCard>? Cards { get; set; }

    public List<BackupCardGroup>? CardGroups { get; set; }

    public List<BackupImageInfo>? Images { get; set; }
}