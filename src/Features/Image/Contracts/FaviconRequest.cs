using System.ComponentModel.DataAnnotations;

namespace Atlas.Features.Image.Contracts;

public class FaviconRequest
{
    /// <summary>目标网页地址，支持 http/https</summary>
    [Required]
    public string Url { get; set; } = string.Empty;
}