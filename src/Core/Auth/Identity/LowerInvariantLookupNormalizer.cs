// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.Identity;

public class LowerInvariantLookupNormalizer : ILookupNormalizer
{
    public string NormalizeEmail(string email)
    {
        return Normalize(email);
    }

    public string NormalizeName(string name)
    {
        return Normalize(name);
    }

    private string Normalize(string key)
    {
        return key?.Normalize().ToLowerInvariant();
    }
}
