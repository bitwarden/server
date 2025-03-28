namespace Bit.Api.AdminConsole.Context;

/// <summary>
/// Current context for the relationship between ProviderUsers and Organizations that they manage.
/// </summary>
public interface IProviderOrganizationContext
{
    /// <summary>
    /// Returns true if the current user is a ProviderUser for the specified organization, false otherwise.
    /// </summary>
    Task<bool> ProviderUserForOrgAsync(Guid orgId);
}
