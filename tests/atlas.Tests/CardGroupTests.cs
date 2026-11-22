using Atlas.Features.Card;
using Atlas.Features.Card.Contracts;
using Atlas.Features.Card.Entities;
using Atlas.Tests.TestSupport;

namespace Atlas.Tests;

/// <summary>分组默认值与卡片归属：启动兜底、分组排序与卡片创建/更新的默认分组</summary>
public class CardGroupTests
{
    [Fact]
    public void 重复建表只保留一个默认分组并把未分组卡片归入其中()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        host.Fsql.Insert(new CardEntity
        {
            Uuid = "legacy", Type = "clock", MinWidth = 1, MinHeight = 1
        }).ExecuteAffrows();

        host.InitializeSchema();

        var groups = host.Fsql.Select<CardGroupEntity>().ToList();
        Assert.Single(groups);
        Assert.Equal(groups[0].Uuid,
            host.Fsql.Select<CardEntity>().Where(c => c.Uuid == "legacy").First().GroupUuid);
    }

    [Fact]
    public async Task 创建分组按当前最大排序追加且更新返回最新状态()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        var service = new CardGroupService(host.Fsql);

        var first = await service.CreateAsync(new CreateCardGroupRequest("g1", "  常用  "));
        var second = await service.CreateAsync(new CreateCardGroupRequest("g2", "其他"));

        Assert.Equal(1, first.SortOrder);
        Assert.Equal(2, second.SortOrder);
        Assert.Equal("常用", first.Name);
        Assert.False(first.Collapsed);

        var updated = await service.UpdateAsync(
            "g1", new UpdateCardGroupRequest("  改名  ", 5, true));
        Assert.NotNull(updated);
        Assert.Equal("改名", updated.Name);
        Assert.Equal(5, updated.SortOrder);
        Assert.True(updated.Collapsed);
        Assert.Null(await service.UpdateAsync(
            "missing", new UpdateCardGroupRequest("x", 0, false)));
    }

    [Fact]
    public async Task 创建与更新卡片未指定分组时落到默认分组()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        var service = new CardService(host.Fsql);
        var defaultUuid = host.Fsql.Select<CardGroupEntity>().First().Uuid;

        var created = await service.CreateCardAsync(new CreateCardRequest(
            "card-1", 0, 0, 1, 1, 0, 0, 1, 1, 1, 1, true, true, "clock", null, ""));
        Assert.Equal(defaultUuid, created.GroupUuid);

        var updated = await service.UpdateCardAsync("card-1", new UpdateCardRequest(
            "card-1", 0, 0, 1, 1, 0, 0, 1, 1, 1, 1, true, true, "clock", null, ""));
        Assert.NotNull(updated);
        Assert.Equal(defaultUuid, updated.GroupUuid);
    }

    [Fact]
    public async Task 删除分组时组内卡片一并删除()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        var service = new CardGroupService(host.Fsql);

        await service.CreateAsync(new CreateCardGroupRequest("g2", "其它"));
        host.Fsql.Insert(new CardEntity
        {
            Uuid = "card-1", GroupUuid = "g2", Type = "clock", MinWidth = 1, MinHeight = 1
        }).ExecuteAffrows();

        Assert.Equal(DeleteCardGroupResult.Deleted, await service.DeleteAsync("g2"));
        Assert.Null(host.Fsql.Select<CardGroupEntity>().Where(g => g.Uuid == "g2").First());
        Assert.Null(host.Fsql.Select<CardEntity>().Where(c => c.Uuid == "card-1").First());
    }

    [Fact]
    public async Task 删除全部分组时卡片一并清空并补回默认分组()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        var service = new CardGroupService(host.Fsql);

        await service.CreateAsync(new CreateCardGroupRequest("g2", "其它"));
        host.Fsql.Insert(new CardEntity
        {
            Uuid = "card-1", GroupUuid = "g2", Type = "clock", MinWidth = 1, MinHeight = 1
        }).ExecuteAffrows();

        await service.DeleteAllAsync();

        var groups = host.Fsql.Select<CardGroupEntity>().ToList();
        Assert.Single(groups);
        Assert.Equal(string.Empty, groups[0].Name);
        Assert.Equal(0, groups[0].SortOrder);
        Assert.Empty(host.Fsql.Select<CardEntity>().ToList());
    }

    [Fact]
    public async Task 删除最后一个分组被拒绝且不存在返回未找到()
    {
        using var host = new TestHost();
        host.InitializeSchema();
        var service = new CardGroupService(host.Fsql);
        var onlyUuid = host.Fsql.Select<CardGroupEntity>().First().Uuid;

        Assert.Equal(DeleteCardGroupResult.LastGroup, await service.DeleteAsync(onlyUuid));
        Assert.Single(host.Fsql.Select<CardGroupEntity>().ToList());
        Assert.Equal(DeleteCardGroupResult.NotFound, await service.DeleteAsync("missing"));
    }
}
