using System.Text.Json;
using System.Text.Json.Serialization;
using OneOf;

namespace Bit.Core.Billing.Payment.Models;

[JsonConverter(typeof(PaymentMethodJsonConverter))]
public class PaymentMethod(OneOf<TokenizedPaymentMethod, NonTokenizedPaymentMethod> input) : OneOfBase<TokenizedPaymentMethod, NonTokenizedPaymentMethod>(input)
{
    public static implicit operator PaymentMethod(TokenizedPaymentMethod tokenized) => new(tokenized);
    public static implicit operator PaymentMethod(NonTokenizedPaymentMethod nonTokenized) => new(nonTokenized);
    public bool IsTokenized => IsT0;
    public bool IsNonTokenized => IsT1;
}

internal class PaymentMethodJsonConverter : JsonConverter<PaymentMethod>
{
    private static readonly string _typePropertyName = "type";

    public override PaymentMethod Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var element = JsonElement.ParseValue(ref reader);

        if (!element.TryGetProperty(options.PropertyNamingPolicy?.ConvertName(_typePropertyName) ?? _typePropertyName, out var typeProperty))
        {
            throw new JsonException(
                $"Failed to deserialize {nameof(PaymentMethod)}: missing '{_typePropertyName}' property");
        }

        var type = typeProperty.GetString();

        // Check if it's a tokenized or non-tokenized type based on the type string
        if (type?.StartsWith("tokenized_", StringComparison.OrdinalIgnoreCase) == true)
        {
            var paymentMethodType = type.Substring("tokenized_".Length);
            if (Enum.TryParse<TokenizablePaymentMethodType>(paymentMethodType, true, out var tokenizedType))
            {
                var token = element.TryGetProperty("token", out var tokenProperty) ? tokenProperty.GetString() : null;

                if (string.IsNullOrEmpty(token))
                {
                    throw new JsonException($"Failed to deserialize tokenized payment method: missing or empty 'token' property");
                }

                return new TokenizedPaymentMethod
                {
                    Type = tokenizedType,
                    Token = token
                };
            }
        }
        else if (type?.StartsWith("non_tokenized_", StringComparison.OrdinalIgnoreCase) == true)
        {
            var paymentMethodType = type.Substring("non_tokenized_".Length);
            if (Enum.TryParse<NonTokenizablePaymentMethodType>(paymentMethodType, true, out var nonTokenizedType))
            {
                return new NonTokenizedPaymentMethod
                {
                    Type = nonTokenizedType
                };
            }
        }

        throw new JsonException($"Failed to deserialize {nameof(PaymentMethod)}: invalid '{_typePropertyName}' value - '{type}'");
    }

    public override void Write(Utf8JsonWriter writer, PaymentMethod value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        value.Switch(
            tokenized =>
            {
                writer.WriteString("type", $"tokenized_{tokenized.Type.ToString().ToLowerInvariant()}");
                writer.WriteString("token", tokenized.Token);
            },
            nonTokenized =>
            {
                writer.WriteString("type", $"non_tokenized_{nonTokenized.Type.ToString().ToLowerInvariant()}");
            }
        );

        writer.WriteEndObject();
    }
}
