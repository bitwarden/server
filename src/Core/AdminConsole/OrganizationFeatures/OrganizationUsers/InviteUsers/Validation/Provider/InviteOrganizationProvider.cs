using Bit.Core.AdminConsole.Enums.Provider;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Provider;

public class InviteOrganizationProvider
{
    public Guid ProviderId { get; init; }
    public ProviderType Type { get; init; }
    public ProviderStatusType Status { get; init; }
    public bool Enabled { get; init; }

    public InviteOrganizationProvider(Entities.Provider.Provider provider)
    {
        ProviderId = provider.Id;
        Type = provider.Type;
        Status = provider.Status;
        Enabled = provider.Enabled;
    }
}
