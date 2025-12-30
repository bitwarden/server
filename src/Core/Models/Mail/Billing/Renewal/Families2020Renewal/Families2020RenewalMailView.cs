using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Models.Mail.Billing.Renewal.Families2020Renewal;

public class Families2020RenewalMailView : BaseMailView
{
    public required string MonthlyRenewalPrice { get; set; }
}

public class Families2020RenewalMail : BaseMail<Families2020RenewalMailView>
{
    public override string Subject { get => "Your Bitwarden Families renewal is updating"; }
}
