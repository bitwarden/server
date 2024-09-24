#nullable enable
namespace Bit.Core.NotificationCenter.Entities;

public class NotificationStatus
{
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public DateTime? ReadDate { get; set; }
    public DateTime? DeletedDate { get; set; }
}
