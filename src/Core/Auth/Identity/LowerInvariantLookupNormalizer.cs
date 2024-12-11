﻿using Microsoft.AspNetCore.Identity;

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

    private static string Normalize(string key)
    {
        return key?.Normalize().ToLowerInvariant();
    }
}
