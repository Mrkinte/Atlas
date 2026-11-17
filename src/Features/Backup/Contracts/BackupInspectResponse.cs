namespace Atlas.Features.Backup.Contracts;

/// <summary>备份包概览，恢复前用来展示可选内容</summary>
public sealed record BackupInspectResponse(
    int Version,
    string AppVersion,
    DateTime ExportedAt,
    string[] Sections,
    int Cards,
    int Images,
    int Live2DConfigs);