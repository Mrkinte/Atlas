using System.Text.Json.Serialization;
using Atlas.Features.Backup.Contracts;
using Atlas.Features.Live2D.Contracts;

namespace Atlas.Features.Backup;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BackupManifest))]
[JsonSerializable(typeof(BackupSettings))]
[JsonSerializable(typeof(BackupLive2D))]
[JsonSerializable(typeof(BackupLive2DConfig))]
[JsonSerializable(typeof(BackupCard))]
[JsonSerializable(typeof(Live2DModelConfigResponse))]
[JsonSerializable(typeof(BackupInspectResponse))]
[JsonSerializable(typeof(BackupRestoreResponse))]
// OpenAPI 生成恢复接口的请求体 schema 时需要 IFormFile 的 JsonTypeInfo，AOT 下必须显式声明
[JsonSerializable(typeof(IFormFile))]
internal partial class BackupJsonContext : JsonSerializerContext;

public static class BackupJsonContextRegistration
{
    public static IServiceCollection AddBackupJsonContext(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, BackupJsonContext.Default));
        return services;
    }
}