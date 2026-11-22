using Atlas.Features.Card.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Features.Card;

public static class CardGroupEndpoints
{
    public static IEndpointRouteBuilder MapCardGroupEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/card-groups").WithTags("CardGroups");

        group.MapGet("/", GetAllGroups).AllowAnonymous();
        group.MapPost("/", CreateGroup).RequireAuthorization();
        group.MapPut("/{groupId}", UpdateGroup).RequireAuthorization();
        group.MapDelete("/", DeleteAllGroups).RequireAuthorization();
        group.MapDelete("/{groupId}", DeleteGroup).RequireAuthorization();

        return endpoints;
    }

    private static async Task<Ok<List<CardGroupResponse>>> GetAllGroups(
        CardGroupService service, CancellationToken ct)
    {
        var groups = await service.GetAllAsync(ct);
        return TypedResults.Ok(groups);
    }

    private static async Task<Created<CardGroupResponse>> CreateGroup(
        CreateCardGroupRequest request, CardGroupService service, CancellationToken ct)
    {
        var group = await service.CreateAsync(request, ct);
        return TypedResults.Created($"/api/card-groups/{group.Uuid}", group);
    }

    private static async Task<Results<Ok<CardGroupResponse>, ProblemHttpResult>> UpdateGroup(
        string groupId, UpdateCardGroupRequest request, CardGroupService service,
        CancellationToken ct)
    {
        var group = await service.UpdateAsync(groupId, request, ct);
        return group is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound)
            : TypedResults.Ok(group);
    }

    private static async Task<NoContent> DeleteAllGroups(
        CardGroupService service, CancellationToken ct)
    {
        await service.DeleteAllAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteGroup(
        string groupId, CardGroupService service, CancellationToken ct)
    {
        return await service.DeleteAsync(groupId, ct) switch
        {
            DeleteCardGroupResult.Deleted => TypedResults.NoContent(),
            DeleteCardGroupResult.LastGroup => TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "无法删除最后一个分组"),
            _ => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound)
        };
    }
}
