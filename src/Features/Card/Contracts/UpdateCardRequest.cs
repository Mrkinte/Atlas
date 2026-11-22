using System.Text.Json.Nodes;

namespace Atlas.Features.Card.Contracts;

public sealed record UpdateCardRequest(
    string Uuid,
    int SmX,
    int SmY,
    int SmWidth,
    int SmHeight,
    int LgX,
    int LgY,
    int LgWidth,
    int LgHeight,
    int MinWidth,
    int MinHeight,
    bool IsVisible,
    bool MobileVisible,
    string Type,
    JsonNode? Data,
    string GroupUuid);