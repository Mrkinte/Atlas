namespace Atlas.Features.Card.Contracts;

public sealed record UpdateCardGroupRequest(
    string Name,
    int SortOrder,
    bool Collapsed);
