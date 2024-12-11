namespace Bit.Core.Models.BitStripe;

// Stripe's SubscriptionListOptions model has a complex input for date filters.
// It expects a dictionary, and has lots of validation rules around what can have a value and what can't.
// To simplify this a bit we are extending Stripe's model and using our own date inputs, and building the dictionary they expect JiT.
// ___
// Our model also facilitates selecting all elements in a list, which is unsupported by Stripe's model.
public class StripeSubscriptionListOptions : Stripe.SubscriptionListOptions
{
    public DateTime? CurrentPeriodEndDate { get; set; }
    public string CurrentPeriodEndRange { get; set; } = "lt";
    public bool SelectAll { get; set; }
    public new Stripe.DateRangeOptions CurrentPeriodEnd
    {
        get
        {
            return CurrentPeriodEndDate.HasValue
                ? new Stripe.DateRangeOptions()
                {
                    LessThan = CurrentPeriodEndRange == "lt" ? CurrentPeriodEndDate : null,
                    GreaterThan = CurrentPeriodEndRange == "gt" ? CurrentPeriodEndDate : null,
                }
                : null;
        }
    }

    public Stripe.SubscriptionListOptions ToStripeApiOptions()
    {
        var stripeApiOptions = (Stripe.SubscriptionListOptions)this;

        if (SelectAll)
        {
            stripeApiOptions.EndingBefore = null;
            stripeApiOptions.StartingAfter = null;
        }

        if (CurrentPeriodEndDate.HasValue)
        {
            stripeApiOptions.CurrentPeriodEnd = new Stripe.DateRangeOptions()
            {
                LessThan = CurrentPeriodEndRange == "lt" ? CurrentPeriodEndDate : null,
                GreaterThan = CurrentPeriodEndRange == "gt" ? CurrentPeriodEndDate : null,
            };
        }

        return stripeApiOptions;
    }
}
