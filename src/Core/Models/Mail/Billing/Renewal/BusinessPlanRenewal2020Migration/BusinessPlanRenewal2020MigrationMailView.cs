using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Models.Mail.Billing.Renewal.BusinessPlanRenewal2020Migration;

public class BusinessPlanRenewal2020MigrationMailView : BaseMailView
{
    public required string RenewalDate { get; set; }
    public required int Seats { get; set; }
    public required string PerUserMonthlyPrice { get; set; }
    public required bool IsAnnual { get; set; }
    public required string TotalPrice { get; set; }
    public string TotalPeriod => IsAnnual ? "year" : "month";
    public List<string> DiscountLines { get; set; } = [];
    public bool HasDiscount => DiscountLines.Count > 0;

    /// <summary>Months the cohort's proactive discount applies for; 0 when there's no month-based discount.</summary>
    public long ProactiveDiscountMonths { get; set; }

    /// <summary>Show the loyalty-discount copy only when the proactive discount spans a positive number of months.</summary>
    public bool ShowProactiveDiscountCopy => ProactiveDiscountMonths > 0;

    public string ProactiveDiscountDurationPhrase =>
        ProactiveDiscountMonths == 1 ? "next month" : $"next {ProactiveDiscountMonths} months";
}

public class BusinessPlanRenewal2020MigrationMail : BaseMail<BusinessPlanRenewal2020MigrationMailView>
{
    public override string Subject { get; set; } = "Your Bitwarden subscription price is changing";
}
