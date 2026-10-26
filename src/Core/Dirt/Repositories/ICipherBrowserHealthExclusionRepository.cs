namespace Bit.Core.Dirt.Repositories;

public interface ICipherBrowserHealthExclusionRepository
{
    /// <summary>Excludes the user's personal, not-deleted logins from browser health. Returns how many were excluded.</summary>
    Task<int> ExcludeAsync(IEnumerable<Guid> cipherIds, Guid userId);

    /// <summary>Removes ciphers from the user's exclusions for the browser health report. Returns count of ciphers removed from exclusion.</summary>
    Task<int> UnexcludeAsync(IEnumerable<Guid> cipherIds, Guid userId);
}
