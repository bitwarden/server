namespace Bit.Core.Dirt.Repositories;

public interface ICipherBrowserHealthExclusionRepository
{
    /// <summary>Excludes the user's personal, not-deleted logins from browser health. Returns how many were excluded.</summary>
    /// <param name="cipherIds">The IDs of the ciphers to exclude.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>The number of ciphers excluded.</returns>
    Task<int> ExcludeAsync(IEnumerable<Guid> cipherIds, Guid userId);

    /// <summary>
    /// Removes ciphers from the user's exclusions for the browser health report. Returns count of ciphers removed from exclusion.
    /// </summary>
    /// <param name="cipherIds">The IDs of the ciphers to unexclude.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>The number of ciphers unexcluded.</returns>
    Task<int> UnexcludeAsync(IEnumerable<Guid> cipherIds, Guid userId);
}
