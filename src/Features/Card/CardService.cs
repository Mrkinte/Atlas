using Atlas.Features.Card.Contracts;
using Atlas.Features.Card.Entities;

namespace Atlas.Features.Card;

public class CardService(
    IFreeSql fsql)
{
    /// <summary>未登录时只返回 IsVisible 的卡片，登录后可获取全部</summary>
    public async Task<List<CardResponse>> GetAllCardsAsync(
        bool includeHidden = false, CancellationToken ct = default)
    {
        var query = fsql.Select<CardEntity>();
        if (!includeHidden) query = query.Where(c => c.IsVisible);
        var cards = await query
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
        return cards.ToResponseList();
    }

    public async Task<CardResponse> CreateCardAsync(
        CreateCardRequest request, CancellationToken ct = default)
    {
        var card = new CardEntity
        {
            Uuid = request.Uuid,
            SmX = request.SmX,
            SmY = request.SmY,
            SmWidth = request.SmWidth,
            SmHeight = request.SmHeight,
            LgX = request.LgX,
            LgY = request.LgY,
            LgWidth = request.LgWidth,
            LgHeight = request.LgHeight,
            MinWidth = request.MinWidth,
            MinHeight = request.MinHeight,
            IsVisible = request.IsVisible,
            MobileVisible = request.MobileVisible,
            Type = request.Type,
            Data = request.Data?.ToJsonString()
        };

        // FreeSql 在 ExecuteAffrowsAsync 后会自动回填自增主键到 card.Id
        await fsql.Insert(card).ExecuteAffrowsAsync(ct);

        return card.ToResponse();
    }

    public async Task<CardResponse?> UpdateCardAsync(
        string id, UpdateCardRequest request, CancellationToken ct = default)
    {
        var card = await fsql.Select<CardEntity>()
            .Where(c => c.Uuid == id)
            .FirstAsync(ct);

        if (card is null) return null;

        card.SmX = request.SmX;
        card.SmY = request.SmY;
        card.SmWidth = request.SmWidth;
        card.SmHeight = request.SmHeight;
        card.LgX = request.LgX;
        card.LgY = request.LgY;
        card.LgWidth = request.LgWidth;
        card.LgHeight = request.LgHeight;
        card.MinWidth = request.MinWidth;
        card.MinHeight = request.MinHeight;
        card.IsVisible = request.IsVisible;
        card.MobileVisible = request.MobileVisible;
        card.Type = request.Type;
        card.Data = request.Data?.ToJsonString();

        var rows = await fsql.Update<CardEntity>()
            .SetSource(card)
            .ExecuteAffrowsAsync(ct);

        return rows == 0 ? null : card.ToResponse();
    }

    public async Task<bool> DeleteCardAsync(string id, CancellationToken ct = default)
    {
        var rows = await fsql.Delete<CardEntity>()
            .Where(c => c.Uuid == id)
            .ExecuteAffrowsAsync(ct);
        return rows > 0;
    }

    public async Task<int> DeleteAllCardsAsync(CancellationToken ct = default)
    {
        // FreeSql 不支持无条件的 Delete，使用恒真条件清空整表
        return await fsql.Delete<CardEntity>()
            .Where(c => c.Id > 0)
            .ExecuteAffrowsAsync(ct);
    }
}