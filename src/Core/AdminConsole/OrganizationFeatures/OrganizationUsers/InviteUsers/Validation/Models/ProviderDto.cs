using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;

public class ProviderDto
{
    public Guid ProviderId { get; init; }
    public ProviderType Type { get; init; }
    public ProviderStatusType Status { get; init; }
    public bool Enabled { get; init; }

    public static ProviderDto FromProviderEntity(Provider provider)
    {
        return new ProviderDto { ProviderId = provider.Id, Type = provider.Type, Status = provider.Status, Enabled = provider.Enabled };
    }
}
