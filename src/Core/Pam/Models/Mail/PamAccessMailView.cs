using System.Globalization;
using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Pam.Models.Mail;

/// <summary>The organization and request link every PAM access mail shares.</summary>
public abstract class PamAccessMailView : BaseMailView
{
    private const string _windowFormat = "d MMM yyyy 'at' HH:mm 'UTC'";

    public required string WebVaultUrl { get; init; }

    public required Guid AccessRequestId { get; init; }

    public required string OrganizationName { get; init; }

    /// <summary>The web vault's user-scoped request page.</summary>
    public string Url => $"{WebVaultUrl}/pam/requests/{AccessRequestId}";

    protected static string FormatWindow(DateTime instant) =>
        instant.ToString(_windowFormat, CultureInfo.InvariantCulture);
}
