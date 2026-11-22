using System.Text.Json.Nodes;
using Atlas.Features.Card.Contracts;
using Atlas.Features.Card.Entities;

namespace Atlas.Features.Card;

public static class CardMappings
{
    public static CardResponse ToResponse(this CardEntity card)
    {
        return new CardResponse
        {
            Uuid = card.Uuid,
            SmX = card.SmX,
            SmY = card.SmY,
            SmWidth = card.SmWidth,
            SmHeight = card.SmHeight,
            LgX = card.LgX,
            LgY = card.LgY,
            LgWidth = card.LgWidth,
            LgHeight = card.LgHeight,
            MinWidth = card.MinWidth,
            MinHeight = card.MinHeight,
            IsVisible = card.IsVisible,
            MobileVisible = card.MobileVisible,
            Type = card.Type,
            GroupUuid = card.GroupUuid ?? string.Empty,
            Data = card.Data is null ? null : JsonNode.Parse(card.Data)
        };
    }

    public static List<CardResponse> ToResponseList(this IEnumerable<CardEntity> cards)
    {
        return cards.Select(ToResponse).ToList();
    }
}