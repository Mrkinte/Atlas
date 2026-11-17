using FreeSql.DataAnnotations;

namespace Atlas.Features.Auth.Entities;

/// <summary>
///     已登出（被吊销）Token 的黑名单，以 jti 为主键
/// </summary>
[Table(Name = "RevokedTokens")]
public class RevokedTokenEntity
{
    [Column(IsPrimary = true, StringLength = 64, IsNullable = false)]
    public string Jti { get; set; } = string.Empty;

    /// <summary>Token 的原始过期时间，用于清理过期记录</summary>
    [Column(IsNullable = false)]
    public DateTime ExpiresAt { get; set; }
}