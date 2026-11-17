using System.Text.Json.Serialization;
using Atlas.Features.Settings.Contracts;

namespace Atlas.Features.Settings;

[JsonSerializable(typeof(SettingsResponse))]
[JsonSerializable(typeof(UpdateSettingsRequest))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Live2DOutfitSelection[]))]
internal partial class SettingsJsonContext : JsonSerializerContext;

public static class SettingsJsonContextRegistration
{
    public static IServiceCollection AddSettingsJsonContext(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, SettingsJsonContext.Default));
        return services;
    }
}