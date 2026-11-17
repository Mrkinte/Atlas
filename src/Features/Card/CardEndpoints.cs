using Atlas.Features.Card.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Atlas.Features.Card;

public static class CardEndpoints
{
    public static IEndpointRouteBuilder MapCardEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/cards").WithTags("Cards");

        // 首页对匿名用户开放：匿名只下发 IsVisible 的卡片，登录后可拿到全部卡片；写操作仍需登录
        group.MapGet("/", GetAllCards).AllowAnonymous();
        group.MapPost("/", CreateCard).RequireAuthorization();
        group.MapPut("/{cardId}", UpdateCard).RequireAuthorization();
        group.MapDelete("/", DeleteAllCards).RequireAuthorization();
        group.MapDelete("/{cardId}", DeleteCard).RequireAuthorization();

        return endpoints;
    }

    private static async Task<Ok<List<CardResponse>>> GetAllCards(
        HttpContext httpContext, CardService service, CancellationToken ct
    )
    {
        // AllowAnonymous 只跳过授权，认证中间件仍会解析 Cookie 中的令牌
        var includeHidden = httpContext.User.Identity?.IsAuthenticated == true;
        var responses = await service.GetAllCardsAsync(includeHidden, ct);
        return TypedResults.Ok(responses);
    }

    private static async Task<Created<CardResponse>> CreateCard(
        CreateCardRequest request, CardService service, CancellationToken ct)
    {
        var card = await service.CreateCardAsync(request, ct);
        return TypedResults.Created($"/api/cards/{card.Uuid}", card);
    }

    private static async Task<Results<Ok<CardResponse>, ProblemHttpResult>> UpdateCard(
        string cardId, UpdateCardRequest request, CardService service, CancellationToken ct)
    {
        var card = await service.UpdateCardAsync(cardId, request, ct);
        return card is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status404NotFound)
            : TypedResults.Ok(card);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteCard(
        string cardId, CardService service, CancellationToken ct)
    {
        var ok = await service.DeleteCardAsync(cardId, ct);
        return ok
            ? TypedResults.NoContent()
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
    }

    private static async Task<NoContent> DeleteAllCards(
        CardService service, CancellationToken ct)
    {
        await service.DeleteAllCardsAsync(ct);
        return TypedResults.NoContent();
    }
}