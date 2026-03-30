using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Bit.Api.Utilities;

public class ValidJsonArrayAttribute : ValidationAttribute
{
    public ValidJsonArrayAttribute()
    {
        ErrorMessage = "Value must be a valid JSON array.";
    }

    public override bool IsValid(object? value)
    {
        if (value is not string str)
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(str);
            return doc.RootElement.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
