using FreeSql.DataAnnotations;

namespace Atlas.Features.Image.Entities;

[Table(Name = "Images")]
public class ImageEntity
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string OriginalName { get; set; } = string.Empty;

    /// <summary>image（图片）或 icon（图标）</summary>
    [Column(StringLength = 16)]
    public string Kind { get; set; } = "image";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}