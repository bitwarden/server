namespace Bit.Core.Models.Data.Organizations.OrganizationUsers;

public class OrganizationUserPublicKey
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PublicKey { get; set; }
}
