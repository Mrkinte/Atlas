using Atlas.Features.Auth;
using Atlas.Features.Auth.Contracts;
using Atlas.Features.Demo.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Features.Demo;

public static class DemoEndpoints
{
    public static IEndpointRouteBuilder MapDemoEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/demo").WithTags("Demo");

        // 前端启动先问开关，再决定是否自动登录
        group.MapGet("", GetStatus).AllowAnonymous();
        group.MapPost("/session", CreateSession).AllowAnonymous();

        return endpoints;
    }

    private static Ok<DemoStatusResponse> GetStatus(DemoMode demo)
    {
        return TypedResults.Ok(new DemoStatusResponse(demo.Enabled));
    }

    private static async Task<Results<Ok<LoginResponse>, NotFound>> CreateSession(
        HttpContext httpContext,
        DemoMode demo,
        AuthService authService,
        AuthTokenService tokenService,
        RefreshTokenStore refreshStore,
        CancellationToken ct)
    {
        if (!demo.Enabled) return TypedResults.NotFound();

        var user = await authService.FindByUsernameAsync(DemoDefaults.Username, ct);

        if (user is null) return TypedResults.NotFound();

        var (token, expiresAt) = tokenService.GenerateToken(
            user.Id, user.Username, user.PasswordVersion);

        httpContext.Response.Cookies.Append(
            AuthCookie.NameFor(httpContext.Request),
            token,
            AuthCookie.Options(httpContext.Request, expiresAt));

        // 演示会话给足有效期，避免访客浏览途中被登出
        var refresh = await refreshStore.CreateAsync(user.Id, true, ct);

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
}