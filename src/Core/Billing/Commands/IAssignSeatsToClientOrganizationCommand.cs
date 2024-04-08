using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;

namespace Bit.Core.Billing.Commands;

public interface IAssignSeatsToClientOrganizationCommand
{
    Task AssignSeatsToClientOrganization(
        Provider provider,
        Organization organization,
        int seats);
}
