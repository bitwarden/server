using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Pam.Models.Mail.AccessLeaseRevoked;

/// <summary>
/// The lease holder's notice that someone else revoked their active access before its window ran out.
/// </summary>
/// <remarks>
/// A courtesy, not a control: the lease is already dead and the client has already re-locked by the time this is
/// composed. It is sent only when an operator revoked the lease, so there is no field for who ended it.
///
/// Bounded by zero knowledge in the same way its siblings are: the collection and cipher are named only by
/// ciphertext the server cannot read, and the revocation reason is withheld for the reason
/// <c>AccessRequest.Reason</c> is, being free text that would name the very system being accessed.
/// <see cref="PamAccessMailView.Url" /> carries the holder to it.
/// </remarks>
public class AccessLeaseRevokedView : PamAccessMailView
{
    /// <summary>
    /// When the lease would have ended on its own, in UTC. Always in the future at send time:
    /// <c>RevokeAccessLeaseCommand</c> refuses a lease whose window has already closed.
    /// </summary>
    public required DateTime NotAfter { get; init; }

    public string ScheduledEnd => FormatWindow(NotAfter);
}

public class AccessLeaseRevokedMail : BaseMail<AccessLeaseRevokedView>
{
    public override string Subject { get; set; } = "Your access was revoked";
}
