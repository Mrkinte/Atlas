using System.Text.Json.Serialization;
using Atlas.Features.Live2D.Contracts;
using Atlas.Features.Settings.Contracts;

namespace Atlas.Features.Live2D;

[JsonSerializable(typeof(Live2DModelResponse))]
[JsonSerializable(typeof(List<Live2DModelResponse>))]
[JsonSerializable(typeof(Live2DModelConfigResponse))]
[JsonSerializable(typeof(UpdateLive2DModelConfigRequest))]
[JsonSerializable(typeof(Live2DOutfitSelection[]))]
// OpenAPI 生成上传接口的请求体 schema 时需要 IFormFile 的 JsonTypeInfo，AOT 下必须显式声明
[JsonSerializable(typeof(IFormFile))]
internal partial class Live2DJsonContext : JsonSerializerContext;

public static class Live2DJsonContextRegistration
{
    public static IServiceCollection AddLive2DJsonContext(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, Live2DJsonContext.Default));
        return services;
    }
}