using Bit.Core.Vault.Models.Data;

namespace Bit.Commercial.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IGetLeasedCipherQuery
{
    /// <summary>
    /// Returns the cipher with its complete data, but only if the caller currently holds an active lease for it and
    /// can otherwise access it. Returns null when there is no active lease or the caller cannot access the cipher, so
    /// callers cannot distinguish "no lease" from "no such cipher".
    /// </summary>
    Task<CipherDetails?> GetLeasedCipherAsync(Guid userId, Guid cipherId);
}
