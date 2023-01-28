using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Identity;

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
