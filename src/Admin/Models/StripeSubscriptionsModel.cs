using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.BitStripe;

namespace Bit.Admin.Models;

public class StripeSubscriptionRowModel
{
    public Stripe.Subscription Subscription { get; set; }
    public bool Selected { get; set; }

    public StripeSubscriptionRowModel() { }
    public StripeSubscriptionRowModel(Stripe.Subscription subscription)
    {
        Subscription = subscription;
    }
}

public enum StripeSubscriptionsAction
{
    Search,
    PreviousPage,
    NextPage,
    Export,
    BulkCancel
}

public class StripeSubscriptionsModel : IValidatableObject
{
    public List<StripeSubscriptionRowModel> Items { get; set; }
    public StripeSubscriptionsAction Action { get; set; } = StripeSubscriptionsAction.Search;
    public string Message { get; set; }
    public List<Stripe.Price> Prices { get; set; }
    public List<Stripe.TestHelpers.TestClock> TestClocks { get; set; }
    public StripeSubscriptionListOptions Filter { get; set; } = new StripeSubscriptionListOptions();
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Action == StripeSubscriptionsAction.BulkCancel && Filter.Status != "unpaid")
        {
            yield return new ValidationResult("Bulk cancel is currently only supported for unpaid subscriptions");
        }
    }
}
