using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationConnections.Interfaces;

public interface IValidateBillingSyncKeyCommand
{
    Task<bool> ValidateBillingSyncKeyAsync(Organization organization, string billingSyncKey);
}
