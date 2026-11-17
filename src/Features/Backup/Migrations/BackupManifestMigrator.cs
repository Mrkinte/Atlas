using System.Text.Json.Nodes;
using Atlas.Extensions;

namespace Atlas.Features.Backup.Migrations;

/// <summary>
///     备份版本校验与逐级迁移：低于当前版本按 v1→v2→… 依次升级，
///     高于当前版本直接拒绝，避免旧程序写出不兼容数据
/// </summary>
public sealed class BackupManifestMigrator(
    IEnumerable<IBackupManifestMigration> migrations)
{
    public void Migrate(JsonObject manifest, int version)
    {
        if (version <= 0)
            throw new InvalidRequestException("备份清单版本无效");

        if (version > BackupLimits.CurrentVersion)
            throw new InvalidRequestException(
                $"备份由更新版本的 Atlas 生成（格式 v{version}），请先升级当前程序");

        if (version < BackupLimits.MinimumSupportedVersion)
            throw new InvalidRequestException(
                $"备份格式过旧（v{version}），当前至少支持 v{BackupLimits.MinimumSupportedVersion}");

        var current = version;
        while (current < BackupLimits.CurrentVersion)
        {
            var migration = migrations.FirstOrDefault(item => item.FromVersion == current);

            if (migration is null)
                throw new InvalidRequestException(
                    $"缺少备份格式 v{current} 到 v{current + 1} 的迁移");

            migration.Migrate(manifest);
            current++;
        }

        manifest["version"] = BackupLimits.CurrentVersion;
    }
}