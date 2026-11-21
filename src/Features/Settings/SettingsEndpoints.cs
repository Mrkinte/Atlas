using Atlas.Features.Settings.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Features.Settings;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/settings").WithTags("Settings");

        // 匿名开放：未登录访问首页/登录页时也要应用站点标题与背景图
        group.MapGet("/", GetSettings).AllowAnonymous();
        group.MapPut("/", UpdateSettings).RequireAuthorization();

        return endpoints;
    }

    private static async Task<Ok<SettingsResponse>> GetSettings(
        HttpContext context, SettingsService service, CancellationToken ct)
    {
        var response = await service.GetAsync(ct);

        // 白名单与 Docker socket 路径仅登录用户可见，匿名请求隐藏
        if (context.User.Identity?.IsAuthenticated != true)
        {
            response.PublicHardware = [];
            response.DockerSocket = string.Empty;
        }

        return TypedResults.Ok(response);
    }

    private static async Task<Ok<SettingsResponse>> UpdateSettings(
        UpdateSettingsRequest request, SettingsService service, CancellationToken ct)
    {
        return TypedResults.Ok(await service.UpdateAsync(request, ct));
    }
}