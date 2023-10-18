using Bit.Core.AdminConsole.Enums.Provider;

namespace Bit.Core.AdminConsole.Models.Data.Provider;

public class ProviderOrganizationProviderDetails
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public Guid OrganizationId { get; set; }
    public string ProviderName { get; set; }
    public ProviderType ProviderType { get; set; }
}
