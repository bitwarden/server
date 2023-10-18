using Bit.Core.AdminConsole.Enums.Provider;

namespace Bit.Core.AdminConsole.Models.Data.Provider;

public class ProviderUserProviderDetails
{
    public Guid ProviderId { get; set; }
    public Guid? UserId { get; set; }
    public string Name { get; set; }
    public string Key { get; set; }
    public ProviderUserStatusType Status { get; set; }
    public ProviderUserType Type { get; set; }
    public bool Enabled { get; set; }
    public string Permissions { get; set; }
    public bool UseEvents { get; set; }
    public ProviderStatusType ProviderStatus { get; set; }
}
