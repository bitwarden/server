namespace Bit.Core.Dirt.Repositories;

public interface ICipherBrowserHealthExclusionRepository
{
    /// <summary>Excludes the user's personal, not-deleted logins from browser health, recording the risk category, which must not be empty. Returns how many were excluded.</summary>
    Task<int> ExcludeAsync(IEnumerable<Guid> cipherIds, Guid userId, string riskCategory);
}
