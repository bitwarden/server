using Bit.Core.AdminConsole.Entities;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface ISavePolicyCommand
{
    Task SaveAsync(Policy policy, Guid? savingUserId);
}
