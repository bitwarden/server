using Bit.Core.Billing.Payment.Models;
using Stripe;

namespace Bit.Core.Test.Billing.Extensions;

public static class StripeExtensions
{
    public static bool HasExpansions(this BaseOptions options, params string[] expansions)
        => expansions.All(expansion => options.Expand.Contains(expansion));

    public static bool Matches(this AddressOptions address, BillingAddress billingAddress) =>
        address.Country == billingAddress.Country &&
        address.PostalCode == billingAddress.PostalCode &&
        address.Line1 == billingAddress.Line1 &&
        address.Line2 == billingAddress.Line2 &&
        address.City == billingAddress.City &&
        address.State == billingAddress.State;
}
