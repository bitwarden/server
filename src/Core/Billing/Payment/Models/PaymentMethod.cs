using System.Text.Json;
using System.Text.Json.Serialization;
using OneOf;

namespace Bit.Core.Billing.Payment.Models;

[JsonConverter(typeof(PaymentMethodJsonConverter))]
public class PaymentMethod(OneOf<TokenizedPaymentMethod, NonTokenizedPaymentMethod> input)
    : OneOfBase<TokenizedPaymentMethod, NonTokenizedPaymentMethod>(input)
{
    public static implicit operator PaymentMethod(TokenizedPaymentMethod tokenized) => new(tokenized);
    public static implicit operator PaymentMethod(NonTokenizedPaymentMethod nonTokenized) => new(nonTokenized);
    public bool IsTokenized => IsT0;
    public bool IsNonTokenized => IsT1;
}

internal class PaymentMethodJsonConverter : JsonConverter<PaymentMethod>
{
    public override PaymentMethod Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var element = JsonElement.ParseValue(ref reader);

        if (!element.TryGetProperty("type", out var typeProperty))
        {
            throw new JsonException("PaymentMethod requires a 'type' property");
        }

        var type = typeProperty.GetString();


        if (Enum.TryParse<TokenizablePaymentMethodType>(type, true, out var tokenizedType) &&
            Enum.IsDefined(typeof(TokenizablePaymentMethodType), tokenizedType))
        {
            var token = element.TryGetProperty("token", out var tokenProperty) ? tokenProperty.GetString() : null;
            if (string.IsNullOrEmpty(token))
            {
                throw new JsonException("TokenizedPaymentMethod requires a 'token' property");
            }

            return new TokenizedPaymentMethod { Type = tokenizedType, Token = token };
        }

        if (Enum.TryParse<NonTokenizablePaymentMethodType>(type, true, out var nonTokenizedType) &&
            Enum.IsDefined(typeof(NonTokenizablePaymentMethodType), nonTokenizedType))
        {
            return new NonTokenizedPaymentMethod { Type = nonTokenizedType };
        }

        throw new JsonException($"Unknown payment method type: {type}");
    }

    public override void Write(Utf8JsonWriter writer, PaymentMethod value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        value.Switch(
            tokenized =>
            {
                writer.WriteString("type",
                    tokenized.Type.ToString().ToLowerInvariant()
                );
                writer.WriteString("token", tokenized.Token);
            },
            nonTokenized => { writer.WriteString("type", nonTokenized.Type.ToString().ToLowerInvariant()); }
        );

        writer.WriteEndObject();
    }
}
