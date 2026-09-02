using Bit.Core.Dirt.Reports.Models.Data;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IGetPasskeyDirectoryQuery
{
    /// <summary>
    /// Passkey directory data from the cache or source.
    /// </summary>
    /// <returns>
    /// Enumerable response with entries each representing
    /// a passkey directory entry with domain name, passwordless and MFA support, and
    /// associated instructions. These are domains that may potentially support passkeys for either
    /// Login or Mutli-Factor Authentication. These are matched up with ciphers client-side with link to documentation
    /// to use passkeys.
    /// </returns>
    Task<IEnumerable<PasskeyDirectoryEntry>> GetPasskeyDirectoryAsync();
}
