using Atlas.Extensions;
using Atlas.Features.Image.Contracts;
using Atlas.Features.Image.Entities;

namespace Atlas.Features.Image;

public class ImageService(
    IFreeSql fsql,
    AtlasPaths paths)
{
    /// <summary>10 MB</summary>
    public const long MaxFileSize = 10 * 1024 * 1024;

    public const string KindImage = "image";

    public const string KindIcon = "icon";

    public static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

    public static bool IsValidKind(string? kind)
    {
        return kind is KindImage or KindIcon;
    }

    // 图片与图标分别落在 images、icons 两个子目录
    private string GetKindDirectory(string kind)
    {
        return Path.Combine(paths.UploadsDirectory, kind == KindIcon ? "icons" : "images");
    }

    public async Task<List<ImageResponse>> GetAllImagesAsync(
        string? kind = null, CancellationToken ct = default)
    {
        var query = fsql.Select<ImageEntity>();

        if (!string.IsNullOrEmpty(kind))
            query = kind == KindIcon
                ? query.Where(i => i.Kind == KindIcon)
                : query.Where(i => i.Kind == KindImage);

        var images = await query.OrderBy(i => i.Id).ToListAsync(ct);
        return images.ToResponseList();
    }

    /// <summary>保存上传的图片或图标；大小与类型校验由端点负责</summary>
    public async Task<ImageResponse> UploadImageAsync(
        IFormFile file, string kind = KindImage, CancellationToken ct = default)
    {
        await using var stream = file.OpenReadStream();
        return await SaveImageAsync(
            stream,
            Path.GetExtension(file.FileName).ToLowerInvariant(),
            Path.GetFileName(file.FileName),
            kind,
            ct);
    }

    public Task<ImageResponse> SaveIconAsync(
        Stream content, string extension, string originalName,
        CancellationToken ct = default)
    {
        return SaveImageAsync(content, extension, originalName, KindIcon, ct);
    }

    private async Task<ImageResponse> SaveImageAsync(
        Stream content, string extension, string originalName,
        string kind, CancellationToken ct)
    {
        // 随机文件名避免重名覆盖，同时不暴露用户原始文件名
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var folder = kind == KindIcon ? "icons" : "images";

        var directory = GetKindDirectory(kind);
        Directory.CreateDirectory(directory);
        var fullPath = Path.Combine(directory, storedName);

        await using (var stream = File.Create(fullPath))
        {
            await content.CopyToAsync(stream, ct);
        }

        var image = new ImageEntity
        {
            Name = storedName,
            RelativePath = $"uploads/{folder}/{storedName}",
            OriginalName = originalName,
            Kind = kind,
            CreatedAt = DateTime.Now
        };

        // ExecuteIdentityAsync 返回自增主键，需手动回填到实体
        image.Id = (int)await fsql.Insert(image).ExecuteIdentityAsync(ct);

        return image.ToResponse();
    }

    public async Task<bool> DeleteImageAsync(int id, CancellationToken ct = default)
    {
        var image = await fsql.Select<ImageEntity>()
            .Where(i => i.Id == id)
            .FirstAsync(ct);

        if (image is null) return false;

        var rows = await fsql.Delete<ImageEntity>()
            .Where(i => i.Id == id)
            .ExecuteAffrowsAsync(ct);

        if (rows == 0) return false;

        // 只取文件名，按记录的类型定位到对应子目录
        var fullPath = Path.Combine(
            GetKindDirectory(image.Kind),
            Path.GetFileName(image.RelativePath));

        if (File.Exists(fullPath)) File.Delete(fullPath);

        return true;
    }
}