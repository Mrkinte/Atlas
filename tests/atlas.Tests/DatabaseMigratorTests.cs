using Atlas.Features.Card.Entities;
using Atlas.Features.Database;
using Atlas.Tests.TestSupport;

namespace Atlas.Tests;

/// <summary>
///     数据库迁移回归保护：真实 SQLite 上验证基线写入、版本推进与失败回滚
///     迁移写后不可逆，回滚语义被破坏时旧数据可能卡在半改状态
/// </summary>
public class DatabaseMigratorTests
{
    private const string VersionKey = "Schema:Version";

    private static int ReadVersion(TestHost host)
    {
        // 版本号由 DatabaseMigrator 的私有 ConfigEntity 写入，测试用原生 SQL 读取，不依赖内部类型
        var value = host.Scalar($"SELECT ConfigValue FROM Configs WHERE ConfigKey = '{VersionKey}'");
        return value is null ? 0 : Convert.ToInt32(value);
    }

    [Fact]
    public void 空库建表后写入基线版本且重复执行幂等()
    {
        using var host = new TestHost();
        host.InitializeSchema();

        var migrator = new DatabaseMigrator(host.Fsql, []);

        migrator.Migrate();
        Assert.Equal(1, ReadVersion(host));

        // 再次执行不应报错，也不应推进版本
        migrator.Migrate();
        Assert.Equal(1, ReadVersion(host));
    }

    [Fact]
    public void 迁移按版本升序执行且只执行高于当前版本的迁移()
    {
        using var host = new TestHost();
        host.InitializeSchema();

        // 乱序注册也按版本号升序依次执行
        new DatabaseMigrator(host.Fsql, [new MarkerMigration(9), new MarkerMigration(5)]).Migrate();
        Assert.Equal(9, ReadVersion(host));
        Assert.NotNull(host.Scalar("SELECT ConfigValue FROM Configs WHERE ConfigKey = 'Rollback:Marker5'"));
        Assert.NotNull(host.Scalar("SELECT ConfigValue FROM Configs WHERE ConfigKey = 'Rollback:Marker9'"));

        // 低于当前版本的迁移跳过
        new DatabaseMigrator(host.Fsql, [new MarkerMigration(3)]).Migrate();
        Assert.Equal(9, ReadVersion(host));
        Assert.Null(host.Scalar("SELECT ConfigValue FROM Configs WHERE ConfigKey = 'Rollback:Marker3'"));
    }

    [Fact]
    public void 迁移中途失败时事务回滚不破坏数据()
    {
        using var host = new TestHost();
        host.InitializeSchema();

        // v5 成功提交，v7 写入标记行并加列后崩溃——v7 的全部改动必须被回滚
        var migrations = new IDatabaseMigration[] { new MarkerMigration(5), new ExplodingMigration() };
        var migrator = new DatabaseMigrator(host.Fsql, migrations);
        Assert.Throws<InvalidOperationException>(migrator.Migrate);

        // 已完成的 v5 已提交；失败迁移的版本号不再推进
        Assert.Equal(5, ReadVersion(host));
        Assert.Null(host.Scalar("SELECT ConfigValue FROM Configs WHERE ConfigKey = 'Rollback:Marker7'"));
        // SQLite 的 DDL 同样在事务内：故障迁移加的列必须消失
        Assert.DoesNotContain(
            "BoomColumn", host.QueryFirstColumn("SELECT name FROM pragma_table_info('Settings')"));

        // 回滚后数据库仍可正常读写，不会被半改状态卡死
        host.Fsql.Insert(new CardEntity { Uuid = "after-fail", Type = "test" }).ExecuteAffrows();
        Assert.Equal(1, host.Fsql.Select<CardEntity>().Count());
    }

    /// <summary>测试替身：写入版本标记行，模拟真实迁移的「改数据 + 推版本」</summary>
    private sealed class MarkerMigration : IDatabaseMigration
    {
        public MarkerMigration(int version)
        {
            Version = version;
        }

        public int Version { get; }

        public string Description => $"测试标记迁移 v{Version}";

        public void Up(IFreeSql fsql)
        {
            fsql.Ado.ExecuteNonQuery(
                $"INSERT INTO Configs (ConfigKey, ConfigValue) VALUES ('Rollback:Marker{Version}', '1')");
        }
    }

    /// <summary>测试替身：先写标记行并加一列，再抛异常，模拟迁移中途崩溃</summary>
    private sealed class ExplodingMigration : IDatabaseMigration
    {
        public int Version => 7;

        public string Description => "测试注入的故障迁移";

        public void Up(IFreeSql fsql)
        {
            fsql.Ado.ExecuteNonQuery(
                "INSERT INTO Configs (ConfigKey, ConfigValue) VALUES ('Rollback:Marker7', '1')");
            fsql.Ado.ExecuteNonQuery("ALTER TABLE Settings ADD COLUMN BoomColumn TEXT");
            throw new InvalidOperationException("模拟迁移中途失败");
        }
    }
}