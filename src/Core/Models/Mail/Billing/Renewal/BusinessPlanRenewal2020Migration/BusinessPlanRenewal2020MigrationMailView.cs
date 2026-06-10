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
}

public class BusinessPlanRenewal2020MigrationMail : BaseMail<BusinessPlanRenewal2020MigrationMailView>
{
    public override string Subject { get; set; } = "Your Bitwarden subscription price is changing";
}
