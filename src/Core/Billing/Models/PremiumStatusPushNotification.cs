namespace Bit.Core.Billing.Models;

public class PremiumStatusPushNotification
{
    public Guid UserId { get; set; }
    public bool Premium { get; set; }
}
