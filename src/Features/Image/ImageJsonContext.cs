using System.Text.Json.Serialization;
using Atlas.Features.Image.Contracts;

namespace Atlas.Features.Image;

[JsonSerializable(typeof(ImageResponse))]
[JsonSerializable(typeof(List<ImageResponse>))]
[JsonSerializable(typeof(FaviconRequest))]
// OpenAPI 生成上传接口的请求体 schema 时需要 IFormFile 的 JsonTypeInfo，AOT 下必须显式声明
[JsonSerializable(typeof(IFormFile))]
internal partial class ImageJsonContext : JsonSerializerContext;

public static class ImageJsonContextRegistration
{
    public static IServiceCollection AddImageJsonContext(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, ImageJsonContext.Default));
        return services;
    }
}