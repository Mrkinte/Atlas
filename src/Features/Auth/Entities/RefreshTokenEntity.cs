using FreeSql.DataAnnotations;

namespace Atlas.Features.Auth.Entities;

/// <summary>
///     刷新令牌，以令牌哈希为主键，原始令牌不落库
/// </summary>
[Table(Name = "RefreshTokens")]
public class RefreshTokenEntity
{
    [Column(IsPrimary = true, StringLength = 64, IsNullable = false)]
    public string TokenHash { get; set; } = string.Empty;

    [Column(IsNullable = false)] public int UserId { get; set; }

    /// <summary>true 时滑动续期 30 天，false 时空闲 2 小时后过期</summary>
    [Column(IsNullable = false)]
    public bool RememberMe { get; set; }

    [Column(IsNullable = false)] public DateTime ExpiresAt { get; set; }

    /// <summary>同一次登录派生的刷新令牌共享，检测到重用时整族吊销</summary>
    [Column(StringLength = 32, IsNullable = true)]
    public string? FamilyId { get; set; }

    /// <summary>
    ///     被轮换的时间（Unix 毫秒），null 表示尚未使用。用时间戳而非 DateTime，
    ///     避免 SQLite 读回时的时区/Kind 偏移影响宽限期判断
    /// </summary>
    [Column(IsNullable = true)]
    public long? ConsumedAtUnixMs { get; set; }

    /// <summary>轮换后新令牌的哈希，仅用于审计</summary>
    [Column(StringLength = 64, IsNullable = true)]
    public string? ReplacedByHash { get; set; }
}