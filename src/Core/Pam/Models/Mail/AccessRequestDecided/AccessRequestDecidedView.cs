using System.Diagnostics.CodeAnalysis;
using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Pam.Models.Mail.AccessRequestDecided;

/// <summary>
/// The requester's answer: their pending access request was approved or denied. One view and one template branching
/// on <see cref="Approved" />, since splitting the two verdicts would let their shared half drift.
/// </summary>
/// <remarks>
/// Bounded by zero knowledge in the same way <c>AccessRequestPendingView</c> is: the collection and cipher are named
/// only by ciphertext the server cannot read. The approver's comment is withheld for the same reason
/// <c>AccessRequest.Reason</c> is, being free text that would name the system being accessed.
/// <see cref="PamAccessMailView.Url" /> carries the recipient to all of it.
/// </remarks>
public class AccessRequestDecidedView : PamAccessMailView
{
    /// <summary>
    /// An approval is not access: it stays a startable approval until the requester activates it
    /// (<c>ActivateAccessRequestCommand</c> mints the lease), so the approved body must not say access has begun.
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
    /// Takes the view rather than being object-initialised like its neighbours because the subject carries the
    /// verdict, and <see cref="Subject" /> is a stored property that cannot derive from <see cref="BaseMail{T}.View" />.
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
