﻿using Bit.Core.Billing.Constants;
using Stripe;

namespace Bit.Core.Billing.Extensions;

public static class CustomerExtensions
{

    /// <summary>
    /// Determines if a Stripe customer supports automatic tax
    /// </summary>
    /// <param name="customer"></param>
    /// <returns></returns>
    public static bool HasTaxLocationVerified(this Customer customer) =>
        customer?.Tax?.AutomaticTax == StripeConstants.AutomaticTaxStatus.Supported;
}
