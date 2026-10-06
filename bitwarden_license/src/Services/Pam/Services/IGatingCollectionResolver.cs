namespace Bit.Services.Pam.Services;

public interface IGatingCollectionResolver
{
    /// <summary>
    /// The organization's collection ids gated by a currently-enabled access rule.
    /// </summary>
    /// <remarks>
    /// Resolved from the organization's rules and collections rather than a caller's, so an administrator
    /// assigned to nothing still resolves the full gated set.
    /// </remarks>
    Task<ISet<Guid>> GetGatingCollectionIdsAsync(Guid organizationId);
}
