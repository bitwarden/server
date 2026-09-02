// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Api.Models.Request;

public class SubscriptionCancellationRequestModel
{
    public string Reason { get; set; }
    public string Feedback { get; set; }
}
