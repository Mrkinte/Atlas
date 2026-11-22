using Atlas.Features.Card.Contracts;
using Atlas.Features.Card.Entities;

namespace Atlas.Features.Card;

public enum DeleteCardGroupResult
{
    Deleted,
    NotFound,
    LastGroup
}

public class CardGroupService(IFreeSql fsql)
{
    public async Task<List<CardGroupResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var groups = await fsql.Select<CardGroupEntity>()
            .OrderBy(g => new { g.SortOrder, g.Id })
            .ToListAsync(ct);
        return groups.Select(ToResponse).ToList();
    }

    public async Task<CardGroupResponse> CreateAsync(
        CreateCardGroupRequest request, CancellationToken ct = default)
    {
        var maxSortOrder = await fsql.Select<CardGroupEntity>()
            .OrderByDescending(g => g.SortOrder)
            .FirstAsync(g => (int?)g.SortOrder, ct);

        var group = new CardGroupEntity
        {
            Uuid = request.Uuid,
            Name = (request.Name ?? string.Empty).Trim(),
            SortOrder = (maxSortOrder ?? -1) + 1,
            Collapsed = false
        };

        await fsql.Insert(group).ExecuteAffrowsAsync(ct);
        return ToResponse(group);
    }

    public async Task<CardGroupResponse?> UpdateAsync(
        string groupId, UpdateCardGroupRequest request, CancellationToken ct = default)
    {
        var group = await fsql.Select<CardGroupEntity>()
            .Where(g => g.Uuid == groupId)
            .FirstAsync(ct);

        if (group is null) return null;

        group.Name = (request.Name ?? string.Empty).Trim();
        group.SortOrder = request.SortOrder;
        group.Collapsed = request.Collapsed;

        var rows = await fsql.Update<CardGroupEntity>()
            .SetSource(group)
            .ExecuteAffrowsAsync(ct);

        return rows == 0 ? null : ToResponse(group);
    }

    public Task DeleteAllAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        fsql.Transaction(() =>
        {
            // FreeSql 不支持无条件的 Delete，使用恒真条件清空整表
            fsql.Delete<CardEntity>()
                .Where(c => c.Id > 0)
                .ExecuteAffrows();
            fsql.Delete<CardGroupEntity>()
                .Where(g => g.Id > 0)
                .ExecuteAffrows();

            // 分组是卡片归属的必需载体，清空后立即补一个默认分组，避免首页无分组可放卡片
            fsql.Insert(new CardGroupEntity
            {
                Uuid = Guid.NewGuid().ToString("N"),
                Name = string.Empty,
                SortOrder = 0,
                Collapsed = false
            }).ExecuteAffrows();
        });

        return Task.CompletedTask;
    }

    public async Task<DeleteCardGroupResult> DeleteAsync(
        string groupId, CancellationToken ct = default)
    {
        var group = await fsql.Select<CardGroupEntity>()
            .Where(g => g.Uuid == groupId)
            .FirstAsync(ct);

        if (group is null) return DeleteCardGroupResult.NotFound;

        var remaining = await fsql.Select<CardGroupEntity>()
            .Where(g => g.Uuid != groupId)
            .FirstAsync(ct);

        if (remaining is null) return DeleteCardGroupResult.LastGroup;

        fsql.Transaction(() =>
        {
            fsql.Delete<CardEntity>()
                .Where(c => c.GroupUuid == groupId)
                .ExecuteAffrows();

            fsql.Delete<CardGroupEntity>()
                .Where(g => g.Uuid == groupId)
                .ExecuteAffrows();
        });

        return DeleteCardGroupResult.Deleted;
    }

    private static CardGroupResponse ToResponse(CardGroupEntity group)
    {
        return new CardGroupResponse
        {
            Uuid = group.Uuid,
            Name = group.Name,
            SortOrder = group.SortOrder,
            Collapsed = group.Collapsed
        };
    }
}
