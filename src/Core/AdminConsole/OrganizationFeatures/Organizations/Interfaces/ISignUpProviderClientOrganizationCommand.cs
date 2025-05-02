using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

public interface ISignUpProviderClientOrganizationCommand
{
    /// <summary>
    /// Sign up a new client organization for a provider.
    /// </summary>
    /// <param name="signup">The signup information.</param>
    /// <returns>A tuple containing the new organization and its default collection.</returns>
    Task<(Organization organization, Collection defaultCollection)> SignUpClientOrganizationAsync(OrganizationSignup signup);
}
