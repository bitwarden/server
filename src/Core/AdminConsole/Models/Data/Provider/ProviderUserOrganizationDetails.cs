using Bit.Core.AdminConsole.Enums.Provider;

namespace Bit.Core.AdminConsole.Models.Data.Provider;

public class ProviderUserOrganizationDetails : BaseUserOrganizationDetails
{
    public new Guid ProviderId { get; set; }
    public new ProviderType ProviderType { get; set; }
    public Guid ProviderUserId { get; set; }
    public ProviderUserStatusType Status { get; set; }
    public ProviderUserType Type { get; set; }
}
