#nullable enable

using Bit.Core;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Seeder.Services;

/// <summary>
/// Service for protecting sensitive database fields using ASP.NET Core Data Protection.
/// Uses the same protection scheme as Bitwarden's production services.
/// </summary>
public class DataProtectionService : IDataProtectionService
{
    private readonly IDataProtector _protector;

    public DataProtectionService(IDataProtectionProvider dataProtectionProvider)
    {
        // Use the same purpose as Bitwarden for compatibility
        _protector = dataProtectionProvider.CreateProtector(Constants.DatabaseFieldProtectorPurpose);
    }

    public string Protect(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        // If already protected, return as-is
        if (value.StartsWith(Constants.DatabaseFieldProtectedPrefix))
        {
            return value;
        }

        // Protect the value and add the prefix
        var protectedValue = _protector.Protect(value);
        return Constants.DatabaseFieldProtectedPrefix + protectedValue;
    }

    public string Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        // If not protected, return as-is
        if (!value.StartsWith(Constants.DatabaseFieldProtectedPrefix))
        {
            return value;
        }

        // Remove prefix and unprotect
        var protectedData = value.Substring(Constants.DatabaseFieldProtectedPrefix.Length);
        return _protector.Unprotect(protectedData);
    }
}
