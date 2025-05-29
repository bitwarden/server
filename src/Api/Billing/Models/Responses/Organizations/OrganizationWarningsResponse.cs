#nullable enable
namespace Bit.Api.Billing.Models.Responses.Organizations;

public record OrganizationWarningsResponse
{
    public FreeTrialWarning? FreeTrial { get; set; }
    public InactiveSubscriptionWarning? InactiveSubscription { get; set; }
    public ResellerRenewalWarning? ResellerRenewal { get; set; }

    public record FreeTrialWarning
    {
        public int RemainingTrialDays { get; set; }
    }

    public record InactiveSubscriptionWarning
    {
        public required string Resolution { get; set; }
    }

    public record ResellerRenewalWarning
    {
        public required string Type { get; set; }
        public UpcomingRenewal? Upcoming { get; set; }
        public IssuedRenewal? Issued { get; set; }
        public PastDueRenewal? PastDue { get; set; }

        public record UpcomingRenewal
        {
            public required DateTime RenewalDate { get; set; }
        }

        public record IssuedRenewal
        {
            public required DateTime IssuedDate { get; set; }
            public required DateTime DueDate { get; set; }
        }

        public record PastDueRenewal
        {
            public required DateTime SuspensionDate { get; set; }
        }
    }
}
