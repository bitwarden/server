using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;

namespace Bit.Core.AdminConsole.Providers.Interfaces;

public interface IRemoveOrganizationFromProviderCommand
{
    Task RemoveOrganizationFromProvider(
        Provider provider,
        ProviderOrganization providerOrganization,
        Organization organization
    );
}
