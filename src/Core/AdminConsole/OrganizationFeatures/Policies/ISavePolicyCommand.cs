using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface ISavePolicyCommand
{
    Task SaveAsync(PolicyUpdate policy);
}
