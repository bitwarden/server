namespace Bit.Core.Models.Mail;

public class OrganizationSeatsAutoscaledViewModel : BaseMailModel
{
    public Guid OrganizationId { get; set; }
    public int InitialSeatCount { get; set; }
    public int CurrentSeatCount { get; set; }
}
