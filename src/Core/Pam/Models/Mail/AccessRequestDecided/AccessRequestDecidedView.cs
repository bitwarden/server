using System.Diagnostics.CodeAnalysis;
using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Pam.Models.Mail.AccessRequestDecided;

/// <summary>
/// Tells the requester their request was approved or denied. Names no collection or cipher and omits the approver's
/// comment.
/// </summary>
public class AccessRequestDecidedView : PamAccessMailView
{
    public required bool Approved { get; init; }

    public required DateTime NotBefore { get; init; }

    public required DateTime NotAfter { get; init; }

    public string WindowStart => FormatWindow(NotBefore);

    public string WindowEnd => FormatWindow(NotAfter);
}

public class AccessRequestDecidedMail : BaseMail<AccessRequestDecidedView>
{
    [SetsRequiredMembers]
    public AccessRequestDecidedMail(string toEmail, AccessRequestDecidedView view)
    {
        ToEmails = [toEmail];
        View = view;
        Subject = view.Approved ? "Your access request was approved" : "Your access request was denied";
    }

    public override string Subject { get; set; }
}
