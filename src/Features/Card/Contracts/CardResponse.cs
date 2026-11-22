using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

namespace Atlas.Features.Card.Contracts;

public class CardResponse
{
    [Required] public string Uuid { get; set; } = string.Empty;

    [Required] public int SmX { get; set; }

    [Required] public int SmY { get; set; }

    [Required] public int SmWidth { get; set; }

    [Required] public int SmHeight { get; set; }

    [Required] public int LgX { get; set; }

    [Required] public int LgY { get; set; }

    [Required] public int LgWidth { get; set; }

    [Required] public int LgHeight { get; set; }

    [Required] public int MinWidth { get; set; }

    [Required] public int MinHeight { get; set; }

    [Required] public bool IsVisible { get; set; }

    [Required] public bool MobileVisible { get; set; }

    [Required] public string Type { get; set; } = string.Empty;

    [Required] public string GroupUuid { get; set; } = string.Empty;

    public JsonNode? Data { get; set; }
}