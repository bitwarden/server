namespace Bit.Core.Models.Mail;

public class OrganizationSeatsMaxReachedViewModel : BaseMailModel
{
    public Guid OrganizationId { get; set; }
    public int MaxSeatCount { get; set; }
}
