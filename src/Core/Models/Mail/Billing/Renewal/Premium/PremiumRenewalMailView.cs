using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Models.Mail.Billing.Renewal.Premium;

public class PremiumRenewalMailView : BaseMailView
{
    public required string BaseMonthlyRenewalPrice { get; set; }
    public required string DiscountedMonthlyRenewalPrice { get; set; }
    public required string DiscountAmount { get; set; }
}

public class PremiumRenewalMail : BaseMail<PremiumRenewalMailView>
{
    public override string Subject { get; set; } = "Your Bitwarden Premium renewal is updating";
}
