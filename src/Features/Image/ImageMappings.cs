using Atlas.Features.Image.Contracts;
using Atlas.Features.Image.Entities;

namespace Atlas.Features.Image;

public static class ImageMappings
{
    public static ImageResponse ToResponse(this ImageEntity image)
    {
        return new ImageResponse
        {
            Id = image.Id,
            OriginalName = image.OriginalName,
            Kind = image.Kind,
            Url = $"/{image.RelativePath}"
        };
    }

    public static List<ImageResponse> ToResponseList(this IEnumerable<ImageEntity> images)
    {
        return images.Select(ToResponse).ToList();
    }
}