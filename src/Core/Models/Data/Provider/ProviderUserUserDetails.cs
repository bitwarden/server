using Bit.Core.Enums.Provider;

namespace Bit.Core.Models.Data;

public class ProviderUserUserDetails
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public Guid? UserId { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public ProviderUserStatusType Status { get; set; }
    public ProviderUserType Type { get; set; }
    public string Permissions { get; set; }
}
