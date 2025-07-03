#nullable enable
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Stripe;

namespace Bit.Core.Billing.Payment.Queries;

public interface IGetBillingAddressQuery
{
    Task<BillingAddress?> Run(ISubscriber subscriber);
}

public class GetBillingAddressQuery(
    ISubscriberService subscriberService) : IGetBillingAddressQuery
{
    public async Task<BillingAddress?> Run(ISubscriber subscriber)
    {
        var productUsageType = subscriber.GetProductUsageType();

        var options = productUsageType switch
        {
            ProductUsageType.Business => new CustomerGetOptions { Expand = ["tax_ids"] },
            _ => new CustomerGetOptions()
        };

        var customer = await subscriberService.GetCustomer(subscriber, options);

        if (customer is not { Address: { Country: not null, PostalCode: not null } })
        {
            return null;
        }

        var taxId = productUsageType == ProductUsageType.Business ? customer.TaxIds?.FirstOrDefault() : null;

        return taxId != null
            ? BillingAddress.From(customer.Address, taxId)
            : BillingAddress.From(customer.Address);
    }
}
