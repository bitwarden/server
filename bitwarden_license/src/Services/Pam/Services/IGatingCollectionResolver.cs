namespace Bit.Services.Pam.Services;

public interface IGatingCollectionResolver
{
    /// <summary>
    /// The organization's collection ids gated by an enabled access rule. Resolved org-wide rather than per caller, so
    /// an administrator assigned to no collection still gets the full set.
    /// </summary>
    Task<ISet<Guid>> GetGatingCollectionIdsAsync(Guid organizationId);
}
