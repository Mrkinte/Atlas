namespace Atlas.Features.Database;

/// <summary>
///     版本化迁移执行器：已应用的版本号保存在 Configs 表，每次启动只执行
///     版本号大于当前记录的迁移，DDL 与版本写入在同一事务内
///     当前结构为 v1 基线，首次启动只记录版本号
/// </summary>
public sealed class DatabaseMigrator(
    IFreeSql fsql,
    IEnumerable<IDatabaseMigration> migrations)
{
    private const int BaselineVersion = 1;

    private const string VersionKey = "Schema:Version";

    public void Migrate()
    {
        var applied = ReadVersion();

        if (applied < BaselineVersion)
        {
            WriteVersion(BaselineVersion);
            applied = BaselineVersion;
        }

        foreach (var migration in migrations
                     .OrderBy(item => item.Version)
                     .Where(item => item.Version > applied))
        {
            fsql.Transaction(() =>
            {
                migration.Up(fsql);
                WriteVersion(migration.Version);
            });

            applied = migration.Version;
        }
    }

    private int ReadVersion()
    {
        var entity = fsql.Select<ConfigEntity>()
            .Where(item => item.ConfigKey == VersionKey)
            .First();

        return entity is not null
               && int.TryParse(entity.ConfigValue, out var version)
            ? version
            : 0;
    }

    private void WriteVersion(int version)
    {
        fsql.InsertOrUpdate<ConfigEntity>()
            .SetSource(new ConfigEntity
            {
                ConfigKey = VersionKey,
                ConfigValue = version.ToString()
            })
            .ExecuteAffrows();
    }
}