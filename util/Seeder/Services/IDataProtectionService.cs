#nullable enable

namespace Bit.Seeder.Services;

/// <summary>
/// Service for protecting sensitive database fields using ASP.NET Core Data Protection
/// </summary>
public interface IDataProtectionService
{
    /// <summary>
    /// Protects a value for storage in the database
    /// </summary>
    /// <param name="value">The value to protect</param>
    /// <returns>Protected value with "P|" prefix, or original value if null/empty</returns>
    string Protect(string? value);
    
    /// <summary>
    /// Unprotects a value retrieved from the database
    /// </summary>
    /// <param name="value">The protected value</param>
    /// <returns>Unprotected value, or original value if not protected</returns>
    string Unprotect(string? value);
}