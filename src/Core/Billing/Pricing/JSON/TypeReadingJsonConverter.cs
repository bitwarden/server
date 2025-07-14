using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Billing.Pricing.Models;

namespace Bit.Core.Billing.Pricing.JSON;

#nullable enable

public abstract class TypeReadingJsonConverter<T> : JsonConverter<T> where T : class
{
    protected virtual string TypePropertyName => nameof(ScalableDTO.Type).ToLower();

    protected string? ReadType(Utf8JsonReader reader)
    {
        while (reader.Read())
        {
            if (reader.CurrentDepth != 1 ||
                reader.TokenType != JsonTokenType.PropertyName ||
                reader.GetString()?.ToLower() != TypePropertyName)
            {
                continue;
            }

            reader.Read();
            return reader.GetString();
        }

        return null;
    }

    protected T? Skip(ref Utf8JsonReader reader)
    {
        reader.Skip();
        return null;
    }
}
