using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

public interface IOrganizationInitiateDeleteCommand
{
    Task InitiateDeleteAsync(Organization organization, string orgAdminEmail);
}
