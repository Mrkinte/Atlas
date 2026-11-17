namespace Atlas.Features.Auth;

/// <summary>
///     HttpOnly 认证 Cookie 的名称与选项，登录、登出与 JWT 校验共用
/// </summary>
public static class AuthCookie
{
    public const string Name = "atlas_token";

    // 刷新令牌 Cookie，由 /api/auth/refresh 读取并轮换
    public const string RefreshName = "atlas_refresh";

    /// <summary>
    ///     明文 HTTP 下的名称后缀。Secure Cookie 不允许被非安全来源的同名 Cookie 覆盖
    ///     （RFC 6265bis），两种协议同名时，从 HTTPS 切回 HTTP 后浏览器会丢弃新 Cookie，
    ///     表现为登录成功但后续请求始终未认证
    /// </summary>
    private const string InsecureSuffix = "_http";

    public static string NameFor(HttpRequest request)
    {
        return request.IsHttps ? Name : Name + InsecureSuffix;
    }

    public static string RefreshNameFor(HttpRequest request)
    {
        return request.IsHttps ? RefreshName : RefreshName + InsecureSuffix;
    }

    /// <summary>
    ///     HTTPS 下标记 Secure，明文 HTTP 下不标记。未配置证书时站点只能走 HTTP，
    ///     强制 Secure 会让浏览器直接丢弃 Cookie，登录返回成功但后续请求全部 401
    /// </summary>
    public static CookieOptions Options(HttpRequest request, DateTime? expiresAt = null)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = expiresAt is null ? null : new DateTimeOffset(expiresAt.Value)
        };
    }
}