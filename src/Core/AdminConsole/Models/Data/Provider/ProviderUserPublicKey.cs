namespace Bit.Core.AdminConsole.Models.Data.Provider;

public class ProviderUserPublicKey
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PublicKey { get; set; }
}
