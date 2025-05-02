using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

public interface IResellerClientOrganizationSignUpCommand
{
    Task<(Organization organization, Collection defaultCollection)> SignupClientAsync(OrganizationSignup signup);
}
