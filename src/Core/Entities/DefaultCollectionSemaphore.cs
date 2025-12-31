namespace Bit.Core.Entities;

public class DefaultCollectionSemaphore
{
    public Guid OrganizationUserId { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
}
