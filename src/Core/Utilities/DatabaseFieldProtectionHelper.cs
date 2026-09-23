using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

#nullable enable

namespace Bit.Core.Utilities;

/// <summary>
/// Shared protect/unprotect logic for the legacy "P|"-prefixed database field protection scheme,
/// used by both the Dapper and EF Core data access implementations.
/// </summary>
public static class DatabaseFieldProtectionHelper
{
    /// <summary>
    /// Protects <paramref name="value"/> unless it is already protected. A value is only treated as
    /// already-protected when it both carries the prefix and can actually be unprotected by
    /// <paramref name="dataProtector"/>. A caller-supplied string that merely starts with the prefix
    /// (but isn't real protector output) is otherwise stored verbatim, unencrypted, and permanently
    /// unreadable on the next read.
    /// </summary>
    public static string? Protect(IDataProtector dataProtector, string? value)
    {
        if (value == null)
        {
            return value;
        }

        if (value.StartsWith(Constants.DatabaseFieldProtectedPrefix))
        {
            var payload = value.Substring(Constants.DatabaseFieldProtectedPrefix.Length);
            try
            {
                dataProtector.Unprotect(payload);
                return value;
            }
            catch (CryptographicException)
            {
                // Not real protector output despite the prefix; fall through and protect it.
            }
        }

        return string.Concat(Constants.DatabaseFieldProtectedPrefix, dataProtector.Protect(value));
    }

    public static string? Unprotect(IDataProtector dataProtector, string? value)
    {
        if (value == null || !value.StartsWith(Constants.DatabaseFieldProtectedPrefix))
        {
            return value;
        }

        return dataProtector.Unprotect(value.Substring(Constants.DatabaseFieldProtectedPrefix.Length));
    }
}
