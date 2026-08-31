using System.Globalization;
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
/// <see cref="Url" /> carries the holder to it.
/// </remarks>
public class AccessLeaseRevokedView : BaseMailView
{
    /// <summary>
    /// UTC is spelled out in the rendered string: there is no per-recipient timezone on this path, so an unqualified
    /// instant would be read as local time and misstate how much of the window was cut short.
    /// </summary>
    private const string _windowFormat = "d MMM yyyy 'at' HH:mm 'UTC'";

    public required string WebVaultUrl { get; init; }

    /// <summary>The request the ended lease was minted from, which is what <see cref="Url" /> addresses.</summary>
    public required Guid AccessRequestId { get; init; }

    public required string OrganizationName { get; init; }

    /// <summary>
    /// When the lease would have ended on its own, in UTC. Always in the future at send time:
    /// <c>RevokeAccessLeaseCommand</c> refuses a lease whose window has already closed.
    /// </summary>
    public required DateTime NotAfter { get; init; }

    public string ScheduledEnd => NotAfter.ToString(_windowFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// The request this lease came from, where the revocation is recorded with whatever reason the operator gave.
    /// The user-scoped PAM pages mount at <c>pam</c> (<c>apps/web/src/app/oss-routing.module.ts:687</c>) with the
    /// request page at <c>requests/:id</c> beneath it (<c>access-requests-routing.module.ts:47</c>). The
    /// organization-scoped tree under <c>/organizations/:organizationId/pam</c> is a different route tree and does
    /// not serve this page.
    /// </summary>
    public string Url => $"{WebVaultUrl}/pam/requests/{AccessRequestId}";
}

public class AccessLeaseRevokedMail : BaseMail<AccessLeaseRevokedView>
{
    public override string Subject { get; set; } = "Your access was revoked";
}
