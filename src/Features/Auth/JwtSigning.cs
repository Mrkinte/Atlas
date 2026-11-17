namespace Atlas.Features.Auth;

public static class JwtDefaults
{
    public const string Issuer = "Atlas";

    public const string Audience = "Atlas.Client";

    public const int AccessTokenExpiresMinutes = 30;

    public const int RememberMeExpiresDays = 30;

    public const int SessionIdleHours = 2;

    // 密码版本声明，改密后 +1，校验端比对不一致即失效
    public const string PasswordVersionClaim = "pwd_ver";
}

public class JwtKeyProvider
{
    private volatile string? _key;

    public void SetKey(string key)
    {
        _key = key;
    }

    public string GetKey()
    {
        return _key
               ?? throw new InvalidOperationException(
                   "JWT 签名密钥尚未初始化，请确认 AppInitializer 已在启动时执行。");
    }
}