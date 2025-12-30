using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Models.Mail.Billing.Renewal.Families2019Renewal;

public class Families2019RenewalMailView : BaseMailView
{
    public required string BaseMonthlyRenewalPrice { get; set; }
    public required string BaseAnnualRenewalPrice { get; set; }
    public required string DiscountedAnnualRenewalPrice { get; set; }
    public required string DiscountAmount { get; set; }
}

public class Families2019RenewalMail : BaseMail<Families2019RenewalMailView>
{
    public override string Subject { get => "Your Bitwarden Families renewal is updating"; }
}
