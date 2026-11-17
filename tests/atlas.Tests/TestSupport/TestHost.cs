using Atlas.Extensions;
using Atlas.Features.Auth;
using Atlas.Features.Backup;
using Atlas.Features.Backup.Migrations;
using Atlas.Features.Database;
using Atlas.Features.Demo;
using Atlas.Features.Settings;
using Atlas.Features.SystemStatus;
using FreeSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SQLitePCL;

namespace Atlas.Tests.TestSupport;

/// <summary>
///     单个测试独享的迷你宿主：临时数据目录 + 真实 SQLite 文件库 + 与 Program.cs 相同的服务组合
///     每个测试用唯一临时目录，互不干扰，可并行
/// </summary>
internal sealed class TestHost : IDisposable
{
    static TestHost()
    {
        // 与 Program.cs 一致：访问数据库前先加载原生 e_sqlite3，重复调用幂等
        Batteries_V2.Init();
    }

    public TestHost()
    {
        DataDirectory = CreateEmptyDirectory();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Atlas:DataDirectory"] = DataDirectory }).Build();
        Paths = AtlasPaths.Resolve(configuration, DataDirectory);

        Fsql = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={Path.Combine(DataDirectory, "atlas.db")}")
            .UseAdoConnectionPool(true)
            // 生产环境由 AppInitializer.EnsureSchema 显式同步表结构，测试沿用同一行为
            .UseAutoSyncStructure(false)
            .Build();

        SystemStatus = new SystemStatusRuntime();
        Settings = new SettingsService(Fsql, SystemStatus);
        Initializer = new AppInitializer(
            Fsql, new JwtKeyProvider(),
            new DatabaseMigrator(Fsql, []),
            new DemoMode(configuration),
            NullLogger<AppInitializer>.Instance);
        // 与 ServiceCollectionExtensions 相同的组合方式：门面组合导出、检查与恢复
        var migrator = new BackupManifestMigrator([]);
        Backup = new BackupService(
            new BackupExportService(Fsql, Settings, Paths),
            new BackupRestoreService(Fsql, Settings, SystemStatus, Paths, migrator),
            new BackupInspectService(migrator));
    }

    /// <summary>本次测试独享的数据目录（含数据库与 uploads）</summary>
    public string DataDirectory { get; }

    public string DatabaseFile => Path.Combine(DataDirectory, "atlas.db");

    public AtlasPaths Paths { get; }

    public IFreeSql Fsql { get; }

    public SystemStatusRuntime SystemStatus { get; }

    public SettingsService Settings { get; }

    public AppInitializer Initializer { get; }

    public BackupService Backup { get; }

    public void Dispose()
    {
        // 先释放 FreeSql（关闭连接池），否则 SQLite 文件被占用，删不掉临时目录
        try
        {
            if (Fsql is IDisposable disposable) disposable.Dispose();
        }
        catch
        {
            // 清理失败不影响测试结论
        }

        try
        {
            Directory.Delete(DataDirectory, true);
        }
        catch
        {
            // 万一仍被占用，遗留的随机临时目录无害
        }
    }

    /// <summary>建表 + 版本迁移，与生产启动路径一致</summary>
    public void InitializeSchema()
    {
        Initializer.EnsureSchema();
    }

    public AuthService CreateAuthService()
    {
        return new AuthService(Fsql, new RefreshTokenStore(Fsql, NullLogger<RefreshTokenStore>.Instance));
    }

    /// <summary>执行标量查询，供测试读取 Configs 版本号等内部状态</summary>
    public object? Scalar(string sql)
    {
        return Fsql.Ado.ExecuteScalar(sql);
    }

    /// <summary>取查询结果第一列，用于检查 sqlite_master / pragma_table_info</summary>
    public List<string> QueryFirstColumn(string sql)
    {
        return Fsql.Ado.Query<string>(sql);
    }

    private static string CreateEmptyDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "atlas-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}