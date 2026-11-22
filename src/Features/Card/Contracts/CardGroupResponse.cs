using System.ComponentModel.DataAnnotations;

namespace Atlas.Features.Card.Contracts;

public class CardGroupResponse
{
    [Required] public string Uuid { get; set; } = string.Empty;

    [Required] public string Name { get; set; } = string.Empty;

    [Required] public int SortOrder { get; set; }

    [Required] public bool Collapsed { get; set; }
}
