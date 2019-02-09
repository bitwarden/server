using System;
using Bit.Core.Enums;
using Bit.Core.Services;

namespace Bit.Core.Models.Table
{
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
    }
}
