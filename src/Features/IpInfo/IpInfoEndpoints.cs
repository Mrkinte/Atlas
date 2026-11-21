using Atlas.Features.Demo;
using Atlas.Features.IpInfo.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Features.IpInfo;

public static class IpInfoEndpoints
{
    /// <summary>演示站点例地址</summary>
    private static readonly IpInfoResponse DemoInfo = new(
        [
            new PublicIpEntry("203.0.113.42", 4, "中国", "上海", "上海", "中国电信", "AS4812"),
            new PublicIpEntry("2001:db8::42", 6, "中国", "北京", "北京", "中国联通", "AS4808")
        ],
        "192.168.1.100",
        49,
        3,
        false);

    public static IEndpointRouteBuilder MapIpInfoEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        // 首页卡片默认匿名可见；未登录只返回打码地址，完整信息需要登录
        endpoints.MapGet("/api/ip", GetIpInfo)
            .WithTags("IpInfo")
            .AllowAnonymous();

        return endpoints;
    }

    private static async Task<Results<Ok<IpInfoResponse>, ProblemHttpResult>> GetIpInfo(
        IpInfoService ipInfoService,
        HttpContext context,
        DemoMode demo,
        bool? refresh,
        CancellationToken ct)
    {
        if (demo.Enabled) return TypedResults.Ok(DemoInfo);

        try
        {
            var includeDetails = context.User.Identity?.IsAuthenticated == true;
            return TypedResults.Ok(
                await ipInfoService.GetAsync(refresh == true, includeDetails, ct));
        }
        catch (IpInfoUnavailableException e)
        {
            return TypedResults.Problem(
                detail: e.Message,
                statusCode: StatusCodes.Status502BadGateway,
                title: "获取公网 IP 失败");
        }
    }
}
