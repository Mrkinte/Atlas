using System.Security.Claims;
using Atlas.Features.Auth.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Atlas.Features.Auth;

public static class AuthEndpoints
{
    // 用户不存在时也跑一次 BCrypt，避免用响应时间枚举用户名
    private static readonly string DummyPasswordHash =
        BCrypt.Net.BCrypt.HashPassword("atlas-dummy-password", 12);

    public static IEndpointRouteBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Auth");

        // 匿名：登录、查询状态、刷新令牌；其余端点受兜底授权策略保护
        group.MapPost("/login", Login).AllowAnonymous().RequireIpRateLimit(IpRateLimitPolicies.Auth);
        group.MapGet("/status", GetStatus).AllowAnonymous();
        group.MapPost("/refresh", Refresh).AllowAnonymous().RequireIpRateLimit(IpRateLimitPolicies.Refresh);
        // 匿名可调用：访问令牌过期时授权中间件会先拒绝请求，刷新令牌就没法吊销了，
        // 所以登出自己处理两种凭证
        group.MapPost("/logout", Logout).AllowAnonymous();
        group.MapPost("/reset-password", ResetPassword).RequireAuthorization();
        group.MapPost("/change-username", ChangeUsername).RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult, ProblemHttpResult>>
        Login(
            LoginRequest request,
            HttpContext httpContext,
            AuthService authService,
            AuthTokenService tokenService,
            RefreshTokenStore refreshStore,
            LoginAttemptLimiter loginLimiter,
            CancellationToken ct)
    {
        // 空密码进 BCrypt 会抛异常
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest);

        var username = request.Username.Trim();

        // 单账号锁定，补 IP 限流覆盖不到的暴力破解
        if (loginLimiter.IsBlocked(username, out var retryAfter))
        {
            httpContext.Response.Headers["Retry-After"] =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
            return TypedResults.Problem(statusCode: StatusCodes.Status429TooManyRequests);
        }

        var user = await authService.FindByUsernameAsync(username, ct);

        // 用户不存在时也校验假哈希，两条分支耗时接近，避免枚举用户名
        var passwordOk = BCrypt.Net.BCrypt.Verify(
            request.Password, user?.PasswordHash ?? DummyPasswordHash);

        // 用户不存在或密码错误统一返回 401，不泄露用户是否存在
        if (user is null || !passwordOk)
        {
            loginLimiter.RegisterFailure(username);
            return TypedResults.Unauthorized();
        }

        loginLimiter.Reset(username);

        var (token, expiresAt) = tokenService.GenerateToken(user.Id, user.Username, user.PasswordVersion);
        httpContext.Response.Cookies.Append(
            AuthCookie.NameFor(httpContext.Request),
            token,
            AuthCookie.Options(httpContext.Request, expiresAt));

        var refresh = await refreshStore.CreateAsync(user.Id, request.RememberMe, ct);
        httpContext.Response.Cookies.Append(
            AuthCookie.RefreshNameFor(httpContext.Request),
            refresh.Token,
            AuthCookie.Options(httpContext.Request, refresh.ExpiresAt));

        return TypedResults.Ok(new LoginResponse
        {
            Username = user.Username,
            ExpiresAt = expiresAt
        });
    }

    // 未登录也返回 200，由前端按 isAuthenticated 决定跳转，避免把 401 当错误处理
    private static Ok<AuthStatusResponse> GetStatus(ClaimsPrincipal principal)
    {
        var username = principal.Identity?.IsAuthenticated == true
            ? principal.FindFirstValue(ClaimTypes.Name)
            : null;

        return TypedResults.Ok(new AuthStatusResponse
        {
            IsAuthenticated = username is not null,
            Username = username
        });
    }

    private static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> Refresh(
        HttpContext httpContext,
        AuthService authService,
        AuthTokenService tokenService,
        RefreshTokenStore refreshStore,
        CancellationToken ct)
    {
        if (!httpContext.Request.Cookies.TryGetValue(
                AuthCookie.RefreshNameFor(httpContext.Request), out var refreshToken)
            || string.IsNullOrEmpty(refreshToken))
            return TypedResults.Unauthorized();

        // 旧刷新令牌立即作废；宽限期内重复使用按并发刷新补发，超出宽限期按泄露整族吊销
        var rotation = await refreshStore.RotateAsync(refreshToken, ct);

        if (rotation.Status != RefreshRotationStatus.Rotated || rotation.Result is null)
        {
            // 仅在检测到重用时清 Cookie；普通失效不清，避免并发请求把另一个请求刚下发的新 Cookie 删掉
            if (rotation.Status == RefreshRotationStatus.Reused) DeleteAuthCookies(httpContext);

            return TypedResults.Unauthorized();
        }

        var rotated = rotation.Result;

        var user = await authService.FindByIdAsync(rotated.UserId, ct);

        if (user is null)
        {
            await refreshStore.RevokeAsync(rotated.Token, ct);
            DeleteAuthCookies(httpContext);
            return TypedResults.Unauthorized();
        }

        var (token, expiresAt) = tokenService.GenerateToken(user.Id, user.Username, user.PasswordVersion);

        httpContext.Response.Cookies.Append(
            AuthCookie.NameFor(httpContext.Request),
            token,
            AuthCookie.Options(httpContext.Request, expiresAt));

        httpContext.Response.Cookies.Append(
            AuthCookie.RefreshNameFor(httpContext.Request),
            rotated.Token,
            AuthCookie.Options(httpContext.Request, rotated.ExpiresAt));

        return TypedResults.Ok(new LoginResponse
        {
            Username = user.Username,
            ExpiresAt = expiresAt
        });
    }

    private static async Task<NoContent> Logout(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        RevokedTokenStore store,
        RefreshTokenStore refreshStore,
        CancellationToken ct)
    {
        // 访问令牌仍有效时加入黑名单，已过期则跳过
        var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var hasRefreshToken = httpContext.Request.Cookies.TryGetValue(
                                  AuthCookie.RefreshNameFor(httpContext.Request), out var refreshToken)
                              && !string.IsNullOrEmpty(refreshToken);

        // 两个凭证都不存在时不写任何东西：无事可做，也避免跨站请求顺带清掉 Cookie
        if (string.IsNullOrEmpty(jti) && !hasRefreshToken) return TypedResults.NoContent();

        if (!string.IsNullOrEmpty(jti))
        {
            // 取访问令牌最长有效期作为记录过期时间，供后续清理
            var expiresAt = DateTime.UtcNow.AddMinutes(JwtDefaults.AccessTokenExpiresMinutes);
            await store.RevokeAsync(jti, expiresAt, ct);
        }

        // 刷新令牌一并吊销，避免登出后还能换发访问令牌
        if (hasRefreshToken) await refreshStore.RevokeAsync(refreshToken!, ct);

        DeleteAuthCookies(httpContext);

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, UnauthorizedHttpResult, ProblemHttpResult>> ResetPassword(
        ResetPasswordRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        AuthService authService,
        CancellationToken ct)
    {
        var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(idValue, out var userId)) return TypedResults.Unauthorized();

        if (!PasswordPolicy.IsValid(request.NewPassword) || string.IsNullOrEmpty(request.OldPassword))
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest);

        var user = await authService.FindByIdAsync(userId, ct);

        // 旧密码错误则拒绝，避免拿到 Token 的人直接改密码
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
            return TypedResults.Unauthorized();

        // 密码版本 +1 让旧访问令牌立即失效，刷新令牌全部吊销，改密后必须重新登录
        await authService.ChangeCredentialAsync(
            userId, user.PasswordVersion, request.NewPassword, ct: ct);
        DeleteAuthCookies(httpContext);

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, UnauthorizedHttpResult, ProblemHttpResult>>
        ChangeUsername(
            ChangeUsernameRequest request,
            ClaimsPrincipal principal,
            HttpContext httpContext,
            AuthService authService,
            CancellationToken ct)
    {
        var idValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(idValue, out var userId)) return TypedResults.Unauthorized();

        var username = request.NewUsername?.Trim() ?? string.Empty;

        // 用户名不能为空、不能含空白字符，长度受数据库列（32）限制
        if (username.Length is 0 or > 32 || username.Any(char.IsWhiteSpace)
                                         || string.IsNullOrEmpty(request.Password))
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest);

        var user = await authService.FindByIdAsync(userId, ct);

        // 验证当前密码，避免拿到 Token 的人直接改用户名
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return TypedResults.Unauthorized();

        if (string.Equals(user.Username, username, StringComparison.Ordinal))
            return TypedResults.NoContent();

        var taken = await authService.IsUsernameTakenAsync(username, userId, ct);

        if (taken) return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict);

        // 会话里仍是旧用户名：密码版本 +1 让已签发的访问令牌失效，刷新令牌一并吊销
        await authService.ChangeCredentialAsync(
            userId, user.PasswordVersion, newUsername: username, ct: ct);
        DeleteAuthCookies(httpContext);

        return TypedResults.NoContent();
    }

    private static void DeleteAuthCookies(HttpContext httpContext)
    {
        var options = AuthCookie.Options(httpContext.Request, DateTime.UtcNow.AddDays(-1));
        httpContext.Response.Cookies.Delete(AuthCookie.NameFor(httpContext.Request), options);
        httpContext.Response.Cookies.Delete(AuthCookie.RefreshNameFor(httpContext.Request), options);
    }
}