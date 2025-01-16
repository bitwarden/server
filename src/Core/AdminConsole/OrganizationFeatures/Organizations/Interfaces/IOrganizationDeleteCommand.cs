using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

public interface IOrganizationDeleteCommand
{
    Task DeleteAsync(Organization organization);
}
