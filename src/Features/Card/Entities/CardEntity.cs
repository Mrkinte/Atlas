using FreeSql.DataAnnotations;

namespace Atlas.Features.Card.Entities;

[Table(Name = "Cards")]
public class CardEntity
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public int Id { get; set; }

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

    [Column(StringLength = 32)] public string Type { get; set; } = string.Empty;

    [Column(StringLength = 36)] public string? GroupUuid { get; set; }

    public string? Data { get; set; }
}