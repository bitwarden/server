namespace Bit.Core.Entities;

public class DefaultCollectionSemaphore
{
    public Guid OrganizationId { get; set; }
    public Guid OrganizationUserId { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
}
