using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Atlas.Features.Auth.Entities;

namespace Atlas.Features.Auth;

/// <summary>
///     刷新令牌的签发、轮换与吊销。原始令牌只在签发时返回一次，落库仅存 SHA-256 哈希
/// </summary>
public class RefreshTokenStore(IFreeSql fsql, ILogger<RefreshTokenStore> logger)
{
    // 宽限期内重复使用刚轮换的令牌视为并发刷新；超出则判定泄露，吊销整个会话家族
    private static readonly TimeSpan RotationGrace = TimeSpan.FromSeconds(30);

    /// <summary>签发刷新令牌：「记住我」30 天，普通会话 2 小时滑动过期</summary>
    public async Task<RefreshResult> CreateAsync(int userId, bool rememberMe, CancellationToken ct = default)
    {
        var (token, expiresAt) = Issue(rememberMe);

        await fsql.Insert(new RefreshTokenEntity
            {
                TokenHash = Hash(token),
                UserId = userId,
                RememberMe = rememberMe,
                ExpiresAt = expiresAt,
                FamilyId = NewFamilyId()
            })
            .ExecuteAffrowsAsync(ct);

        return new RefreshResult(token, userId, rememberMe, expiresAt);
    }

    /// <summary>作废旧令牌并签发新令牌；令牌无效或检测到重用时返回对应状态</summary>
    public async Task<RefreshRotation> RotateAsync(string token, CancellationToken ct = default)
    {
        var hash = Hash(token);
        var existing = await fsql.Select<RefreshTokenEntity>()
            .Where(t => t.TokenHash == hash)
            .FirstAsync(ct);

        if (existing is null) return RefreshRotation.Invalid;

        if (AsUtc(existing.ExpiresAt) <= DateTime.UtcNow)
        {
            await fsql.Delete<RefreshTokenEntity>()
                .Where(t => t.TokenHash == hash)
                .ExecuteAffrowsAsync(ct);
            return RefreshRotation.Invalid;
        }

        // 已使用过的令牌：宽限期内补发，超出宽限期判定为泄露
        if (existing.ConsumedAtUnixMs is not null)
            return await HandleConsumedAsync(existing, ct);

        var (newToken, expiresAt) = Issue(existing.RememberMe);
        var newHash = Hash(newToken);
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 原子占用：并发轮换同一令牌时，只有一个请求能把 ConsumedAt 从 NULL 改为 now
        var affected = await fsql.Update<RefreshTokenEntity>()
            .Set(t => t.ConsumedAtUnixMs, nowMs)
            .Set(t => t.ReplacedByHash, (string?)newHash)
            .Where(t => t.TokenHash == hash && t.ConsumedAtUnixMs == null)
            .ExecuteAffrowsAsync(ct);

        if (affected == 0)
        {
            // 抢占失败，重新读取后按「已使用」处理
            var latest = await fsql.Select<RefreshTokenEntity>()
                .Where(t => t.TokenHash == hash)
                .FirstAsync(ct);

            return latest is null
                ? RefreshRotation.Invalid
                : await HandleConsumedAsync(latest, ct);
        }

        try
        {
            await fsql.Insert(new RefreshTokenEntity
                {
                    TokenHash = newHash,
                    UserId = existing.UserId,
                    RememberMe = existing.RememberMe,
                    ExpiresAt = expiresAt,
                    FamilyId = existing.FamilyId ?? hash
                })
                .ExecuteAffrowsAsync(ct);
        }
        catch
        {
            // 插入失败则释放占用，避免无故把用户登出
            await fsql.Update<RefreshTokenEntity>()
                .Set(t => t.ConsumedAtUnixMs, null)
                .Set(t => t.ReplacedByHash, null)
                .Where(t => t.TokenHash == hash)
                .ExecuteAffrowsAsync(CancellationToken.None);
            throw;
        }

        // 顺手清理过期记录，避免表无限增长
        await fsql.Delete<RefreshTokenEntity>()
            .Where(t => t.ExpiresAt < DateTime.UtcNow)
            .ExecuteAffrowsAsync(ct);

        return new RefreshRotation(
            RefreshRotationStatus.Rotated,
            new RefreshResult(newToken, existing.UserId, existing.RememberMe, expiresAt));
    }

    private async Task<RefreshRotation> HandleConsumedAsync(
        RefreshTokenEntity consumed, CancellationToken ct)
    {
        var elapsedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        - (consumed.ConsumedAtUnixMs ?? 0);

        // 宽限期内大概率是同一浏览器的并发刷新，同族补发，避免互相踢下线
        if (elapsedMs <= RotationGrace.TotalMilliseconds)
            return await IssueInFamilyAsync(consumed, ct);

        // 超出宽限期仍在使用旧令牌：按泄露处理，吊销整个会话家族
        logger.LogWarning(
            "检测到刷新令牌重用，已吊销会话家族。UserId: {UserId}, FamilyId: {FamilyId}",
            consumed.UserId, consumed.FamilyId);

        await RevokeFamilyAsync(consumed, ct);
        return new RefreshRotation(RefreshRotationStatus.Reused);
    }

    private async Task<RefreshRotation> IssueInFamilyAsync(
        RefreshTokenEntity existing, CancellationToken ct)
    {
        var (token, expiresAt) = Issue(existing.RememberMe);

        await fsql.Insert(new RefreshTokenEntity
            {
                TokenHash = Hash(token),
                UserId = existing.UserId,
                RememberMe = existing.RememberMe,
                ExpiresAt = expiresAt,
                FamilyId = existing.FamilyId ?? existing.TokenHash
            })
            .ExecuteAffrowsAsync(ct);

        return new RefreshRotation(
            RefreshRotationStatus.Rotated,
            new RefreshResult(token, existing.UserId, existing.RememberMe, expiresAt));
    }

    public async Task RevokeAsync(string token, CancellationToken ct = default)
    {
        var hash = Hash(token);
        var existing = await fsql.Select<RefreshTokenEntity>()
            .Where(t => t.TokenHash == hash)
            .FirstAsync(ct);

        if (existing is null) return;

        await RevokeFamilyAsync(existing, ct);
    }

    public async Task RevokeAllForUserAsync(int userId, CancellationToken ct = default)
    {
        await fsql.Delete<RefreshTokenEntity>()
            .Where(t => t.UserId == userId)
            .ExecuteAffrowsAsync(ct);
    }

    private async Task RevokeFamilyAsync(RefreshTokenEntity token, CancellationToken ct)
    {
        // 存量记录没有 FamilyId，退化为只删当前令牌
        if (string.IsNullOrEmpty(token.FamilyId))
        {
            await fsql.Delete<RefreshTokenEntity>()
                .Where(t => t.TokenHash == token.TokenHash)
                .ExecuteAffrowsAsync(ct);
            return;
        }

        await fsql.Delete<RefreshTokenEntity>()
            .Where(t => t.FamilyId == token.FamilyId)
            .ExecuteAffrowsAsync(ct);
    }

    // 按会话类型计算滑动有效期
    private static (string Token, DateTime ExpiresAt) Issue(bool rememberMe)
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        var expiresAt = DateTime.UtcNow.Add(rememberMe
            ? TimeSpan.FromDays(JwtDefaults.RememberMeExpiresDays)
            : TimeSpan.FromHours(JwtDefaults.SessionIdleHours));

        return (token, expiresAt);
    }

    private static string NewFamilyId()
    {
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>SQLite 带 Z 的文本可能被解析成本地时间，直接与 UtcNow 比较会差出时区偏移</summary>
    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string Hash(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    /// <summary>新刷新令牌（原始值仅此一次可见）及其会话信息</summary>
    public sealed record RefreshResult(string Token, int UserId, bool RememberMe, DateTime ExpiresAt);
}

public enum RefreshRotationStatus
{
    /// <summary>成功换发新令牌</summary>
    Rotated,

    /// <summary>令牌不存在或已过期</summary>
    Invalid,

    /// <summary>检测到旧令牌重用，会话家族已吊销</summary>
    Reused
}

/// <summary>成功时携带新令牌，失败时只携带状态</summary>
public sealed record RefreshRotation(RefreshRotationStatus Status, RefreshTokenStore.RefreshResult? Result = null)
{
    public static readonly RefreshRotation Invalid = new(RefreshRotationStatus.Invalid);
}

/// <summary>已吊销 Token（黑名单）的读写，登出接口与 JWT 校验共用</summary>
public class RevokedTokenStore(IFreeSql fsql)
{
    public async Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default)
    {
        var count = await fsql.Select<RevokedTokenEntity>()
            .Where(t => t.Jti == jti)
            .CountAsync(ct);
        return count > 0;
    }

    /// <summary>加入黑名单，并清理已过期的记录</summary>
    public async Task RevokeAsync(string jti, DateTime expiresAt, CancellationToken ct = default)
    {
        // INSERT OR IGNORE 语义：并发重复吊销时静默忽略，不抛主键冲突
        await fsql.InsertOrUpdate<RevokedTokenEntity>()
            .SetSource(new RevokedTokenEntity { Jti = jti, ExpiresAt = expiresAt })
            .IfExistsDoNothing()
            .ExecuteAffrowsAsync(ct);

        // 黑名单保留到 Token 过期即可，顺手清理避免无限增长
        await fsql.Delete<RevokedTokenEntity>()
            .Where(t => t.ExpiresAt < DateTime.UtcNow)
            .ExecuteAffrowsAsync(ct);
    }
}