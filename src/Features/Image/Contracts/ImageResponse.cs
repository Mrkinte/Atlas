using System.ComponentModel.DataAnnotations;

namespace Atlas.Features.Image.Contracts;

public class ImageResponse
{
    [Required] public int Id { get; set; }

    [Required] public string OriginalName { get; set; } = string.Empty;

    [Required] public string Kind { get; set; } = string.Empty;

    [Required] public string Url { get; set; } = string.Empty;
}