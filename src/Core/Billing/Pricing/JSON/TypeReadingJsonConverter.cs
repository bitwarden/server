﻿using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Billing.Pricing.Models;

namespace Bit.Core.Billing.Pricing.JSON;

#nullable enable

public abstract class TypeReadingJsonConverter<T> : JsonConverter<T>
{
    protected virtual string TypePropertyName => nameof(ScalableDTO.Type).ToLower();

    protected string? ReadType(Utf8JsonReader reader)
    {
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.PropertyName || reader.GetString()?.ToLower() != TypePropertyName)
            {
                continue;
            }

            reader.Read();
            return reader.GetString();
        }

        return null;
    }
}
