#nullable enable
using Bit.Core.Billing.Services;
using Bit.Core.Entities;

namespace Bit.Core.Billing.Payment.Queries;

public interface IGetCreditQuery
{
    Task<decimal?> Run(ISubscriber subscriber);
}

public class GetCreditQuery(
    ISubscriberService subscriberService) : IGetCreditQuery
{
    public async Task<decimal?> Run(ISubscriber subscriber)
    {
        var customer = await subscriberService.GetCustomer(subscriber);

        if (customer == null)
        {
            return null;
        }

        return Convert.ToDecimal(customer.Balance) * -1 / 100;
    }
}
