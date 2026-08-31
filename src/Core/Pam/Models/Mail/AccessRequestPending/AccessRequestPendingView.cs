using System.Globalization;
using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Pam.Models.Mail.AccessRequestPending;

/// <summary>
/// One approver's notification that a named requester is waiting on their decision.
/// </summary>
/// <remarks>
/// What may appear here is bounded by zero knowledge, not by copywriting. The collection and the cipher are named
/// only by ciphertext the server cannot read, and <c>AccessRequest.Reason</c> — though stored in the clear — is
/// user-typed free text that would name the very system being accessed. So the body identifies the request by the
/// organization, the requester and the window, and sends the approver to <see cref="Url" /> for everything else.
/// </remarks>
public class AccessRequestPendingView : BaseMailView
{
    /// <summary>
    /// UTC is spelled out in the rendered string. There is no per-recipient timezone on this path, so an
    /// unqualified instant would be read as local time and would misstate the window the approver is granting.
    /// </summary>
    private const string _windowFormat = "d MMM yyyy 'at' HH:mm 'UTC'";

    public required string WebVaultUrl { get; init; }

    public required Guid AccessRequestId { get; init; }

    /// <summary>Plaintext, unlike the collection and cipher names this mail deliberately omits.</summary>
    public required string OrganizationName { get; init; }

    /// <summary>The approver's counterparty on this request, which is why their address may be disclosed here.</summary>
    public required string RequesterEmail { get; init; }

    /// <summary>The start of the requested window, in UTC — <c>AccessRequest.NotBefore</c> is normalised before storage.</summary>
    public required DateTime NotBefore { get; init; }

    /// <summary>The end of the requested window, in UTC — <c>AccessRequest.NotAfter</c> is normalised before storage.</summary>
    public required DateTime NotAfter { get; init; }

    public string WindowStart => Format(NotBefore);

    public string WindowEnd => Format(NotAfter);

    /// <summary>
    /// The approver's view of this one request. The user-scoped PAM pages mount at <c>pam</c>
    /// (<c>apps/web/src/app/oss-routing.module.ts:687</c>) and the request page is
    /// <c>requests/:id</c> beneath it (<c>access-requests-routing.module.ts:47</c>). The organization-scoped
    /// admin surface under <c>/organizations/:organizationId/pam</c> is a different route tree and does not
    /// serve this page.
    /// </summary>
    public string Url => $"{WebVaultUrl}/pam/requests/{AccessRequestId}";

    private static string Format(DateTime instant) => instant.ToString(_windowFormat, CultureInfo.InvariantCulture);
}

public class AccessRequestPendingMail : BaseMail<AccessRequestPendingView>
{
    public override string Subject { get; set; } = "An access request is waiting for your decision";
}
