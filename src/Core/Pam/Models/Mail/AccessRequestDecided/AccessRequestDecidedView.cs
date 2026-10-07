using System.Diagnostics.CodeAnalysis;
using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Pam.Models.Mail.AccessRequestDecided;

/// <summary>
/// Tells the requester their request was approved or denied, in one template so the shared half cannot drift. Names
/// no collection or cipher and withholds the approver's comment, free text that could name the system.
/// </summary>
public class AccessRequestDecidedView : PamAccessMailView
{
    /// <summary>
    /// An approval is not access until the requester activates it, so the approved body must not say access has begun.
    /// </summary>
    public required bool Approved { get; init; }

    public required DateTime NotBefore { get; init; }

    public required DateTime NotAfter { get; init; }

    public string WindowStart => FormatWindow(NotBefore);

    public string WindowEnd => FormatWindow(NotAfter);
}

public class AccessRequestDecidedMail : BaseMail<AccessRequestDecidedView>
{
    /// <summary>
    /// Takes the view, unlike its neighbours, because <see cref="Subject" /> carries the verdict and, as a stored
    /// property, cannot derive from <see cref="BaseMail{T}.View" />.
    /// </summary>
    [SetsRequiredMembers]
    public AccessRequestDecidedMail(string toEmail, AccessRequestDecidedView view)
    {
        ToEmails = [toEmail];
        View = view;
        Subject = view.Approved ? "Your access request was approved" : "Your access request was denied";
    }

    public override string Subject { get; set; }
}
