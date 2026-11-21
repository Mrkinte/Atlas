using Atlas.Features.Demo;
using Atlas.Features.Docker.Contracts;
using Atlas.Features.Settings;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Features.Docker;

public static class DockerEndpoints
{
    public static IEndpointRouteBuilder MapDockerEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/docker").WithTags("Docker");

        group.MapGet("/status", GetStatus).AllowAnonymous();

        return endpoints;
    }

    private static async Task<Ok<DockerStatusResponse>> GetStatus(
        DockerStatusService service,
        SettingsService settingsService,
        DemoMode demo,
        HttpContext context,
        CancellationToken ct)
    {
        var settings = await settingsService.GetAsync(ct);
        if (!settings.DockerEnabled) return TypedResults.Ok(DockerStatusResponse.Disabled());

        var includeDetails = demo.Enabled || context.User.Identity?.IsAuthenticated == true;

        // 演示数据是固定假数据，匿名访客也能看到完整列表
        var snapshot = demo.Enabled
            ? DockerStatusService.CreateDemoSnapshot()
            : await service.GetAsync(settings.DockerSocket, ct);

        return TypedResults.Ok(snapshot.ToResponse(includeDetails));
    }
}
