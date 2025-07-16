using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Entities;

namespace Bit.Core.Billing.Extensions;

public static class SubscriberExtensions
{
    /// <summary>
    /// We are taking only first 30 characters of the SubscriberName because stripe provide for 30 characters  for
    /// custom_fields,see the link: https://stripe.com/docs/api/invoices/create
    /// </summary>
    /// <param name="subscriber"></param>
    /// <returns></returns>
    public static string GetFormattedInvoiceName(this ISubscriber subscriber)
    {
        var subscriberName = subscriber.SubscriberName();

        if (string.IsNullOrWhiteSpace(subscriberName))
        {
            return string.Empty;
        }

        return subscriberName.Length <= 30
            ? subscriberName
            : subscriberName[..30];
    }

    public static ProductUsageType GetProductUsageType(this ISubscriber subscriber)
        => subscriber switch
        {
            User => ProductUsageType.Personal,
            Organization organization when organization.PlanType.GetProductTier() is ProductTierType.Free or ProductTierType.Families => ProductUsageType.Personal,
            Organization => ProductUsageType.Business,
            Provider => ProductUsageType.Business,
            _ => throw new ArgumentOutOfRangeException(nameof(subscriber))
        };
}
