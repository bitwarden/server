using Bit.Core.Providers.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Provider;

public class InviteOrganizationProvider
{
    public Guid ProviderId { get; init; }
    public ProviderType Type { get; init; }
    public ProviderStatusType Status { get; init; }
    public bool Enabled { get; init; }
    public int? Seats { get; init; }

    public InviteOrganizationProvider(Bit.Core.Providers.Entities.Provider provider, int? seats)
    {
        ProviderId = provider.Id;
        Type = provider.Type;
        Status = provider.Status;
        Enabled = provider.Enabled;
        Seats = seats;
    }
}
