using Atlas.Features.Card.Entities;

namespace Atlas.Features.Backup.Contracts;

/// <summary>备份中的卡片分组，字段与业务实体解耦</summary>
public class BackupCardGroup
{
    public string Uuid { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool Collapsed { get; set; }

    internal static BackupCardGroup FromEntity(CardGroupEntity source)
    {
        return new BackupCardGroup
        {
            Uuid = source.Uuid,
            Name = source.Name,
            SortOrder = source.SortOrder,
            Collapsed = source.Collapsed
        };
    }
}
