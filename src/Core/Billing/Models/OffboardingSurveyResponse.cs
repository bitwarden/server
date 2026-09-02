// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Billing.Models;

public class OffboardingSurveyResponse
{
    public Guid UserId { get; set; }
    public string Reason { get; set; }
    public string Feedback { get; set; }
}
