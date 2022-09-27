using Bit.Core.Enums;

namespace Bit.Core.Entities;

public interface ISubscriber
{
    Guid Id { get; }
    GatewayType? Gateway { get; set; }
    string GatewayCustomerId { get; set; }
    string GatewaySubscriptionId { get; set; }
    string BillingEmailAddress();
    string BillingName();
    string BraintreeCustomerIdPrefix();
    string BraintreeIdField();
    string GatewayIdField();
    bool IsUser();
}
