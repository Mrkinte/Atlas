using Atlas.Features.Card.Entities;
using Atlas.Features.Image.Entities;

namespace Atlas.Features.About;

/// <summary>
///     /api/about 的数据访问。端点不直接查询 IFreeSql，统一经过这里
/// </summary>
public class AboutService(IFreeSql fsql)
{
    public async Task<(int CardCount, int ImageCount)> GetCountsAsync(CancellationToken ct = default)
    {
        var cardCount = (int)await fsql.Select<CardEntity>().CountAsync(ct);
        var imageCount = (int)await fsql.Select<ImageEntity>().CountAsync(ct);

        return (cardCount, imageCount);
    }
}