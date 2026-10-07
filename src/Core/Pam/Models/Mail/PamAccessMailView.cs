using System.Globalization;
using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Pam.Models.Mail;

/// <summary>What every PAM access mail shares: the request's organization and a link back to the request.</summary>
public abstract class PamAccessMailView : BaseMailView
{
    /// <summary>
    /// Spells out UTC, since there is no per-recipient timezone here and an unqualified time would read as local.
    /// </summary>
    private const string _windowFormat = "d MMM yyyy 'at' HH:mm 'UTC'";

    public required string WebVaultUrl { get; init; }

    public required Guid AccessRequestId { get; init; }

    public required string OrganizationName { get; init; }

    /// <summary>
    /// The web vault's user-scoped request page, not the organization-scoped <c>/organizations/:organizationId/pam</c>
    /// tree, which does not serve it.
    /// </summary>
    public string Url => $"{WebVaultUrl}/pam/requests/{AccessRequestId}";

    protected static string FormatWindow(DateTime instant) =>
        instant.ToString(_windowFormat, CultureInfo.InvariantCulture);
}
