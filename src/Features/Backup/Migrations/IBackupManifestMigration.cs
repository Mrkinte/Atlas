using System.Text.Json.Nodes;

namespace Atlas.Features.Backup.Migrations;

/// <summary>
///     备份清单的单步迁移：把 <see cref="FromVersion" /> 的清单原地升级到下一版本
/// </summary>
public interface IBackupManifestMigration
{
    /// <summary>迁移前的格式版本，完成后版本号加一</summary>
    int FromVersion { get; }

    /// <summary>在反序列化为当前清单类型前，对原始 JSON 做结构转换</summary>
    void Migrate(JsonObject manifest);
}