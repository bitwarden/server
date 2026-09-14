using System.Globalization;
using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Pam.Models.Mail;

/// <summary>
/// The half every PAM access mail shares: which organization the request belongs to, and the link back to the
/// request itself.
/// </summary>
public abstract class PamAccessMailView : BaseMailView
{
    /// <summary>
    /// UTC is spelled out in the rendered string: there is no per-recipient timezone on this path, so an unqualified
    /// instant would be read as local time and misstate the window.
    /// </summary>
    private const string _windowFormat = "d MMM yyyy 'at' HH:mm 'UTC'";

    public required string WebVaultUrl { get; init; }

    public required Guid AccessRequestId { get; init; }

    public required string OrganizationName { get; init; }

    /// <summary>
    /// The recipient's view of the request. The user-scoped PAM pages mount at <c>pam</c>
    /// (<c>apps/web/src/app/oss-routing.module.ts:687</c>) with the request page at <c>requests/:id</c> beneath it
    /// (<c>access-requests-routing.module.ts:47</c>). The organization-scoped tree under
    /// <c>/organizations/:organizationId/pam</c> is a different route tree and does not serve this page.
    /// </summary>
    public string Url => $"{WebVaultUrl}/pam/requests/{AccessRequestId}";

    protected static string FormatWindow(DateTime instant) =>
        instant.ToString(_windowFormat, CultureInfo.InvariantCulture);
}
