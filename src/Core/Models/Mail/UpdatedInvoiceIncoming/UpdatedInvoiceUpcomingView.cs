using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Models.Mail.UpdatedInvoiceIncoming;

public class UpdatedInvoiceUpcomingView : BaseMailView;

public class UpdatedInvoiceUpcomingMail : BaseMail<UpdatedInvoiceUpcomingView>
{
    public override string Subject { get => "Your Subscription Will Renew Soon"; }
}
