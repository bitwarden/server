using Bit.Core.AdminConsole.Entities;
using Bit.Core.Providers.Entities;

namespace Bit.Core.Providers.Interfaces;

public interface IRemoveOrganizationFromProviderCommand
{
    Task RemoveOrganizationFromProvider(
        Provider provider,
        ProviderOrganization providerOrganization,
        Organization organization);
}
