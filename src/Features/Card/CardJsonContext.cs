using System.Text.Json.Serialization;
using Atlas.Features.Card.Contracts;

namespace Atlas.Features.Card;

[JsonSerializable(typeof(CardResponse))]
[JsonSerializable(typeof(List<CardResponse>))]
[JsonSerializable(typeof(CreateCardRequest))]
[JsonSerializable(typeof(UpdateCardRequest))]
internal partial class CardJsonContext : JsonSerializerContext;

public static class CardJsonContextRegistration
{
    public static IServiceCollection AddCardJsonContext(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, CardJsonContext.Default));
        return services;
    }
}