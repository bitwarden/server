using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
/// <see cref="Url" /> carries the recipient to all of it.
/// </remarks>
public class AccessRequestDecidedView : BaseMailView
{
    /// <summary>
    /// UTC is spelled out in the rendered string: there is no per-recipient timezone on this path, so an unqualified
    /// instant would be read as local time and misstate when an approval stops being startable.
    /// </summary>
    private const string _windowFormat = "d MMM yyyy 'at' HH:mm 'UTC'";

    public required string WebVaultUrl { get; init; }

    public required Guid AccessRequestId { get; init; }

    public required string OrganizationName { get; init; }

    /// <summary>
    /// An approval is not access: it stays a startable approval until the requester activates it
    /// (<c>ActivateAccessRequestCommand</c> mints the lease), so the approved body must not say access has begun.
    /// </summary>
    public required bool Approved { get; init; }

    public required DateTime NotBefore { get; init; }

    public required DateTime NotAfter { get; init; }

    public string WindowStart => Format(NotBefore);

    public string WindowEnd => Format(NotAfter);

    /// <summary>
    /// Where an approval is started and where a denial is read. The user-scoped PAM pages mount at <c>pam</c>
    /// (<c>apps/web/src/app/oss-routing.module.ts:687</c>) with the request page at <c>requests/:id</c> beneath it
    /// (<c>access-requests-routing.module.ts:47</c>). The organization-scoped tree under
    /// <c>/organizations/:organizationId/pam</c> is a different route tree and does not serve this page.
    /// </summary>
    public string Url => $"{WebVaultUrl}/pam/requests/{AccessRequestId}";

    private static string Format(DateTime instant) => instant.ToString(_windowFormat, CultureInfo.InvariantCulture);
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
