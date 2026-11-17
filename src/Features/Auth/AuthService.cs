using Atlas.Features.Auth.Entities;

namespace Atlas.Features.Auth;

/// <summary>
///     用户数据访问与凭据变更。端点与 CLI 命令不直接查询 IFreeSql，统一经过这里
/// </summary>
public class AuthService(IFreeSql fsql, RefreshTokenStore refreshStore)
{
    public async Task<UserEntity?> FindByUsernameAsync(string username, CancellationToken ct = default)
    {
        return await fsql.Select<UserEntity>()
            .Where(u => u.Username == username)
            .FirstAsync(ct);
    }

    public async Task<UserEntity?> FindByIdAsync(int userId, CancellationToken ct = default)
    {
        return await fsql.Select<UserEntity>()
            .Where(u => u.Id == userId)
            .FirstAsync(ct);
    }

    public Task<List<UserEntity>> ListAsync(CancellationToken ct = default)
    {
        return fsql.Select<UserEntity>()
            .OrderBy(u => u.Id)
            .ToListAsync(ct);
    }

    public Task<bool> IsUsernameTakenAsync(string username, int excludeUserId, CancellationToken ct = default)
    {
        return fsql.Select<UserEntity>()
            .Where(u => u.Username == username && u.Id != excludeUserId)
            .AnyAsync(ct);
    }

    /// <summary>
    ///     密码版本 +1 让该用户已签发的访问令牌立即失效，并吊销全部刷新令牌
    ///     改密码传 newPassword，改用户名传 newUsername，两者只传其一
    /// </summary>
    public async Task ChangeCredentialAsync(
        int userId,
        int currentPasswordVersion,
        string? newPassword = null,
        string? newUsername = null,
        CancellationToken ct = default)
    {
        var update = fsql.Update<UserEntity>()
            .Where(u => u.Id == userId);

        if (newPassword is not null)
            update = update.Set(u => u.PasswordHash, BCrypt.Net.BCrypt.HashPassword(newPassword, 12));

        if (newUsername is not null) update = update.Set(u => u.Username, newUsername);

        // 密码版本 +1，该用户已签发的访问令牌全部失效
        await update
            .Set(u => u.PasswordVersion, currentPasswordVersion + 1)
            .ExecuteAffrowsAsync(ct);

        // 刷新令牌一并吊销，改密后必须重新登录
        await refreshStore.RevokeAllForUserAsync(userId, ct);
    }
}