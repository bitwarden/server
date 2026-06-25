using System.Net;
using Bit.Core.Billing.Enums;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Settings;

namespace Bit.Core.Billing.Models.Mail.Mailer;

public class SalesAssistedTrialInvitationEmailView : BaseMailView
{
    private readonly IGlobalSettings _globalSettings;

    public SalesAssistedTrialInvitationEmailView(IGlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    /// <summary>
    /// The signed sales-assisted registration token. URL-encoded by <see cref="Url"/> before use.
    /// </summary>
    public required string Token { get; set; }

    /// <summary>
    /// The prospect's email address. URL-encoded by <see cref="Url"/> before use.
    /// </summary>
    public required string Email { get; set; }

    public required ProductTierType ProductTier { get; set; }

    /// <summary>
    /// Currently we only support one product type at a time, despite Products being a collection.
    /// If we receive both PasswordManager and SecretsManager, we send the user to the PM trial route.
    /// </summary>
    public required IEnumerable<ProductType> Products { get; set; }

    public required int TrialLength { get; set; }

    public required bool PaymentOptional { get; set; }

    public required string SenderEmail { get; set; }

    /// <summary>
    /// The destination URL for the invitation CTA. Mirrors the two-branch new-user routing in
    /// <see cref="Bit.Core.Billing.Models.Mail.TrialInitiationVerifyEmail"/>:
    /// <list type="bullet">
    /// <item>PM trial → <c>trial-initiation</c>;</item>
    /// <item>SM-only trial → <c>secrets-manager-trial-initiation</c>.</item>
    /// </list>
    /// Unlike the legacy <c>HandlebarsMailService</c> flow, the IMailer pattern has no service layer to
    /// URL-encode in, so <see cref="Token"/> and <see cref="Email"/> are encoded here. Failing to encode
    /// silently breaks registration when a token contains <c>+</c> or <c>=</c> characters.
    /// </summary>
    public string Url
    {
        get
        {
            var url = $"{_globalSettings.BaseServiceUri.VaultWithHash}/{Route}" +
                      $"?productTier={(int)ProductTier}" +
                      $"&product={string.Join(",", Products.Select(p => (int)p))}" +
                      $"&trialLength={TrialLength}" +
                      $"&salesAssistedToken={WebUtility.UrlEncode(Token)}" +
                      $"&email={WebUtility.UrlEncode(Email)}";

            if (PaymentOptional)
            {
                url += "&paymentOptional=true";
            }

            url += "&fromEmail=true";

            return url;
        }
    }

    private string Route => Products.Any(p => p == ProductType.PasswordManager)
        ? "trial-initiation"
        : "secrets-manager-trial-initiation";
}

public class SalesAssistedTrialInvitationEmail : BaseMail<SalesAssistedTrialInvitationEmailView>
{
    public override string Subject { get; set; } = "You're invited to try Bitwarden";
}
