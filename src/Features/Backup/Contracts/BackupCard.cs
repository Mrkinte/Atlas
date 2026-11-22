using System.Text.Json.Nodes;
using Atlas.Features.Card.Contracts;

namespace Atlas.Features.Backup.Contracts;

/// <summary>备份中的单张卡片，字段与业务实体解耦</summary>
public class BackupCard
{
    public string Uuid { get; set; } = string.Empty;

    public int SmX { get; set; }

    public int SmY { get; set; }

    public int SmWidth { get; set; }

    public int SmHeight { get; set; }

    public int LgX { get; set; }

    public int LgY { get; set; }

    public int LgWidth { get; set; }

    public int LgHeight { get; set; }

    public int MinWidth { get; set; }

    public int MinHeight { get; set; }

    public bool IsVisible { get; set; }

    public bool MobileVisible { get; set; }

    public string Type { get; set; } = string.Empty;

    public string GroupUuid { get; set; } = string.Empty;

    public JsonNode? Data { get; set; }

    internal static BackupCard FromResponse(CardResponse source)
    {
        return new BackupCard
        {
            Uuid = source.Uuid,
            SmX = source.SmX,
            SmY = source.SmY,
            SmWidth = source.SmWidth,
            SmHeight = source.SmHeight,
            LgX = source.LgX,
            LgY = source.LgY,
            LgWidth = source.LgWidth,
            LgHeight = source.LgHeight,
            MinWidth = source.MinWidth,
            MinHeight = source.MinHeight,
            IsVisible = source.IsVisible,
            MobileVisible = source.MobileVisible,
            Type = source.Type,
            GroupUuid = source.GroupUuid,
            Data = source.Data?.DeepClone()
        };
    }
}