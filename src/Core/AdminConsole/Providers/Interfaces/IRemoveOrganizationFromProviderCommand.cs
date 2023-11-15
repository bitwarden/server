namespace Bit.Core.AdminConsole.Providers.Interfaces;

public interface IRemoveOrganizationFromProviderCommand
{
    Task RemoveOrganizationFromProvider(Guid providerId, Guid providerOrganizationId);
}
