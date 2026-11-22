using FreeSql.DataAnnotations;

namespace Atlas.Features.Card.Entities;

[Table(Name = "CardGroups")]
public class CardGroupEntity
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public int Id { get; set; }

    public string Uuid { get; set; } = string.Empty;

    [Column(StringLength = 32)] public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool Collapsed { get; set; }
}
