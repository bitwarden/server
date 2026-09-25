using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

#nullable enable

namespace Bit.Core.Utilities;

/// <summary>
/// Shared protect/unprotect logic for the "P|"-prefixed database field protection scheme,
/// used by both the Dapper and EF Core data access implementations.
/// </summary>
public static class DatabaseFieldProtectionHelper
{
    /// <summary>
    /// Protects <paramref name="value"/> unless it is already protected. A value is only treated as
    /// already-protected when it can actually be unprotected by <paramref name="dataProtector"/>.
    /// A caller-supplied string that merely starts with the prefix should not be assumed to be data
    /// protected.
    /// </summary>
    public static string? Protect(IDataProtector dataProtector, string? value)
    {
        if (value == null)
        {
            return value;
        }

        var unprotected = Unprotect(dataProtector, value);
        if (!ReferenceEquals(unprotected, value))
        {
            return value;
        }

        try
        {
            return string.Concat(Constants.DatabaseFieldProtectedPrefix, dataProtector.Protect(value));
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Value could not be protected.", ex);
        }
    }

    public static string? Unprotect(IDataProtector dataProtector, string? value)
    {
        if (value == null || !value.StartsWith(Constants.DatabaseFieldProtectedPrefix))
        {
            return value;
        }

        try
        {
            return dataProtector.Unprotect(value.Substring(Constants.DatabaseFieldProtectedPrefix.Length));
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Value could not be unprotected.", ex);
        }
    }
}
