namespace Bit.Core.Dirt.Repositories;

public interface ICipherReportExclusionRepository
{
    /// <summary>Excludes the user's personal, not-deleted logins from health reports. Returns how many were excluded.</summary>
    Task<int> ExcludeAsync(IEnumerable<Guid> cipherIds, Guid userId);
}
