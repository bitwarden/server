using Bit.Core.AdminConsole.Entities;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.Services;

public interface ISavePolicyCommand
{
    Task SaveAsync(Policy policy, IUserService userService, IOrganizationService organizationService,
        Guid? savingUserId);
}
