using System.Security.Cryptography;
using Atlas.Features.Auth;
using Atlas.Features.Auth.Entities;
using Atlas.Features.Card.Entities;
using Atlas.Features.Database;
using Atlas.Features.Demo;
using Atlas.Features.Image.Entities;
using Atlas.Features.Live2D.Entities;
using Atlas.Features.Settings.Entities;
using FreeSql.DataAnnotations;

namespace Atlas;

[Table(Name = "Configs")]
internal class ConfigEntity
{
    [Column(IsPrimary = true, StringLength = 64, IsNullable = false)]
    public string ConfigKey { get; set; } = string.Empty;

    [Column(StringLength = 512, IsNullable = false)]
    public string ConfigValue { get; set; } = string.Empty;
}

public class AppInitializer(
    IFreeSql fsql,
    JwtKeyProvider keyProvider,
    DatabaseMigrator migrator,
    DemoMode demoMode,
    ILogger<AppInitializer> logger)
{
    private const string JwtKeyConfigName = "Jwt:Key";

    private const string DefaultUsername = "atlas";

    private const string DefaultPassword = "atlas@admin";

    /// <summary>
    ///     建表清单的唯一来源，EnsureSchema、实体注册测试与 TrimmerRoots.xml 共用
    ///     新增实体只改这里，漏登导致的 AOT 裁剪问题会被测试拦住
    /// </summary>
    internal static readonly Type[] SchemaEntityTypes =
    [
        typeof(ConfigEntity),
        typeof(UserEntity),
        typeof(RefreshTokenEntity),
        typeof(RevokedTokenEntity),
        typeof(CardEntity),
        typeof(ImageEntity),
        typeof(SettingsEntity),
        typeof(Live2DModelConfigEntity)
    ];

    /// <summary>
    ///     确保依赖的数据表存在（生产环境关闭了 AutoSyncStructure）
    ///     Web 启动与命令行工具共用，后者只需要表结构，不需要播种数据
    /// </summary>
    public void EnsureSchema()
    {
        // 与逐个调用泛型 SyncStructure<T>() 等价：FreeSql 的非泛型重载按同一套实体元数据建表
        fsql.CodeFirst.SyncStructure(SchemaEntityTypes);
    }

    public void Initialize()
    {
        EnsureSchema();

        // 表结构增量同步无法处理删列/改名等破坏性变更，统一交给版本化迁移
        migrator.Migrate();

        // 生成并持久化 JWT 签名密钥，供运行时签发与校验使用
        keyProvider.SetKey(LoadOrCreateJwtKey());

        // 演示站点只播种演示账号，避免默认管理员密码随镜像一起发出去
        if (demoMode.Enabled)
        {
            EnsureDemoUser();
            return;
        }

        // 首次运行时创建管理员账号，使用默认密码
        if (fsql.Select<UserEntity>().Count() == 0)
        {
            fsql.Insert(new UserEntity
            {
                Username = DefaultUsername,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword, 12)
            }).ExecuteAffrows();

            logger.LogWarning(
                "已创建管理员账号 {Username}，默认密码：{Password}。请登录后立即修改密码。",
                DefaultUsername,
                DefaultPassword);
        }
    }

    private void EnsureDemoUser()
    {
        var exists = fsql.Select<UserEntity>()
            .Where(u => u.Username == DemoDefaults.Username)
            .Any();

        if (exists) return;

        // 演示账号不接受密码登录，随机密码只为满足非空约束
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        fsql.Insert(new UserEntity
        {
            Username = DemoDefaults.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12)
        }).ExecuteAffrows();
    }

    /// <summary>从数据库读取 JWT 密钥，不存在时随机生成并写入</summary>
    private string LoadOrCreateJwtKey()
    {
        var existing = fsql.Select<ConfigEntity>()
            .Where(c => c.ConfigKey == JwtKeyConfigName)
            .First();

        if (existing is not null && !string.IsNullOrWhiteSpace(existing.ConfigValue)) return existing.ConfigValue;

        // 48 字节随机数 Base64 后为 64 字符，满足 HS256 对密钥长度的要求
        var generated = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

        fsql.Insert(new ConfigEntity { ConfigKey = JwtKeyConfigName, ConfigValue = generated })
            .ExecuteAffrows();

        return generated;
    }
}