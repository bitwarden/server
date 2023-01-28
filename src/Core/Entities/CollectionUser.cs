namespace Bit.Core.Entities;

public class CollectionUser
{
    public Guid CollectionId { get; set; }
    public Guid OrganizationUserId { get; set; }
    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }
}
