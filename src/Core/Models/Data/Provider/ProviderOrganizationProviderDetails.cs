using Bit.Core.Enums.Provider;

namespace Bit.Core.Models.Data;

public class ProviderOrganizationProviderDetails
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public Guid OrganizationId { get; set; }
    public string ProviderName { get; set; }
    public ProviderType ProviderType { get; set; }
    public ProviderStatusType ProviderStatus { get; set; }
    public string ProviderBillingEmail { get; set; }
}
