using System.ComponentModel.DataAnnotations;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.Utilities;

/// <summary>
/// Validates that a string is a well-formed key id, by deferring to <see cref="KeyId"/> itself so
/// the format lives in one place. Null is considered valid; the key id is always optional on requests.
/// </summary>
public class KeyIdAttribute : ValidationAttribute
{
    public KeyIdAttribute()
        : base("{0} is not a valid key id.")
    { }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not string stringValue)
        {
            return false;
        }

        try
        {
            KeyId.FromHexEncodedString(stringValue);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
