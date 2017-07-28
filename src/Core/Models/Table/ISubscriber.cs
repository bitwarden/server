using Bit.Core.Enums;
using Bit.Core.Services;

namespace Bit.Core.Models.Table
{
    public interface ISubscriber
    {
        GatewayType? Gateway { get; set; }
        string GatewayCustomerId { get; set; }
        string GatewaySubscriptionId { get; set; }
        string BillingEmailAddress();
        string BillingName();
        IPaymentService GetPaymentService(GlobalSettings globalSettings);
    }
}
