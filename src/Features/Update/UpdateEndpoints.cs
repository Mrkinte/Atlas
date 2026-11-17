using Atlas.Features.Update.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Features.Update;

public static class UpdateEndpoints
{
    public static IEndpointRouteBuilder MapUpdateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/update/check", Check)
            .WithTags("Update")
            .RequireAuthorization();

        return endpoints;
    }

    /// <summary>refresh 绕过服务端缓存，服务内部仍保留一分钟的最小检查间隔</summary>
    private static async Task<Results<Ok<UpdateCheckResponse>, ProblemHttpResult>> Check(
        UpdateService updateService,
        [FromQuery] bool? refresh,
        CancellationToken ct)
    {
        try
        {
            return TypedResults.Ok(await updateService.CheckAsync(refresh == true, ct));
        }
        catch (UpdateUnavailableException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
