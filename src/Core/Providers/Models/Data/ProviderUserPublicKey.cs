// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Providers.Models.Data;

public class ProviderUserPublicKey
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PublicKey { get; set; }
}
