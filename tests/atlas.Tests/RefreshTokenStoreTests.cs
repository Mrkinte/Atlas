using System.Security.Cryptography;
using System.Text;
using Atlas.Features.Auth;
using Atlas.Features.Auth.Entities;
using Atlas.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Tests;

/// <summary>
///     刷新令牌轮换回归保护：轮换成功、宽限期补发、超期重用整族吊销
///     令牌族是防会话劫持重放的核心机制，重用检测被削弱时攻击者可静默接管会话
/// </summary>
public class RefreshTokenStoreTests
{
    private static RefreshTokenStore CreateStore(TestHost host)
    {
        return new RefreshTokenStore(host.Fsql, NullLogger<RefreshTokenStore>.Instance);
    }

    /// <summary>落库存的是 SHA-256 大写十六进制，测试按同样规则定位数据库行</summary>
    private static string HashOf(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    [Fact]
    public async Task 新令牌轮换成功且旧令牌被占用()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        var store = CreateStore(host);

        var issued = await store.CreateAsync(7, true);
        var rotated = await store.RotateAsync(issued.Token);

        Assert.Equal(RefreshRotationStatus.Rotated, rotated.Status);
        Assert.NotNull(rotated.Result);
        Assert.NotEqual(issued.Token, rotated.Result!.Token);
        Assert.Equal(7, rotated.Result.UserId);
        Assert.True(rotated.Result.RememberMe);

        var rows = host.Fsql.Select<RefreshTokenEntity>().ToList();
        Assert.Equal(2, rows.Count);
        var old = rows.Single(r => r.TokenHash == HashOf(issued.Token));
        var current = rows.Single(r => r.TokenHash == HashOf(rotated.Result.Token));
        // 同一族：新令牌延续旧令牌的 FamilyId，旧令牌被标记占用并记录替换目标
        Assert.Equal(old.FamilyId, current.FamilyId);
        Assert.NotNull(old.ConsumedAtUnixMs);
        Assert.Equal(current.TokenHash, old.ReplacedByHash);
        Assert.Null(current.ConsumedAtUnixMs);
    }

    [Fact]
    public async Task 宽限期内重用旧令牌按并发刷新补发新令牌()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        var store = CreateStore(host);

        var issued = await store.CreateAsync(7, false);
        var first = await store.RotateAsync(issued.Token);

        // 客户端重复提交（如双击、网络重试）落在宽限期内：补发新令牌而不是判重用吊销，
        // 避免正常用户被误杀——这是有意保留的现有行为
        var grace = await store.RotateAsync(issued.Token);

        Assert.Equal(RefreshRotationStatus.Rotated, grace.Status);
        Assert.NotNull(grace.Result);
        Assert.NotEqual(first.Result!.Token, grace.Result.Token);
        Assert.Equal(3, host.Fsql.Select<RefreshTokenEntity>().Count());
    }

    [Fact]
    public async Task 超出宽限期重用旧令牌触发整族吊销()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        var store = CreateStore(host);

        var issued = await store.CreateAsync(7, false);
        var familyId = host.Fsql.Select<RefreshTokenEntity>()
            .Where(r => r.TokenHash == HashOf(issued.Token))
            .First().FamilyId;
        var rotated = await store.RotateAsync(issued.Token);
        Assert.Equal(RefreshRotationStatus.Rotated, rotated.Status);

        // 把旧令牌的占用时间回拨 60 秒（宽限期 30 秒，留足裕量避免测试时序抖动），
        // 制造"宽限期已过但旧令牌被再次提交"的重用场景
        var staleConsumedAt = DateTimeOffset.UtcNow.AddSeconds(-60).ToUnixTimeMilliseconds();
        host.Fsql.Update<RefreshTokenEntity>()
            .Set(r => r.ConsumedAtUnixMs, staleConsumedAt)
            .Where(r => r.TokenHash == HashOf(issued.Token))
            .ExecuteAffrows();

        var reused = await store.RotateAsync(issued.Token);

        // 重用判定 + 整族吊销：新旧令牌全部失效，攻击者拿到的令牌一并作废
        Assert.Equal(RefreshRotationStatus.Reused, reused.Status);
        Assert.Null(reused.Result);
        Assert.Equal(0, host.Fsql.Select<RefreshTokenEntity>().Count());

        // 吊销动作按整个家族生效，而不是只删被重用的那条
        Assert.Equal(0,
            host.Fsql.Select<RefreshTokenEntity>().Where(r => r.FamilyId == familyId).Count());
    }

    [Fact]
    public async Task 未知或过期令牌返回Invalid()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        var store = CreateStore(host);

        // 数据库里不存在的令牌
        Assert.Equal(RefreshRotationStatus.Invalid, (await store.RotateAsync("unknown-token")).Status);

        // 存在但已过期的令牌：轮换入口顺带清理过期行
        var issued = await store.CreateAsync(7, true);
        host.Fsql.Update<RefreshTokenEntity>()
            .Set(r => r.ExpiresAt, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .Where(r => r.TokenHash == HashOf(issued.Token))
            .ExecuteAffrows();

        Assert.Equal(RefreshRotationStatus.Invalid, (await store.RotateAsync(issued.Token)).Status);
        Assert.Equal(0, host.Fsql.Select<RefreshTokenEntity>().Count());
    }

    [Fact]
    public async Task 登出吊销整个会话家族()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        var store = CreateStore(host);

        var issued = await store.CreateAsync(7, false);
        var rotated = await store.RotateAsync(issued.Token);
        Assert.Equal(2, host.Fsql.Select<RefreshTokenEntity>().Count());

        await store.RevokeAsync(rotated.Result!.Token);

        Assert.Equal(0, host.Fsql.Select<RefreshTokenEntity>().Count());
    }
}