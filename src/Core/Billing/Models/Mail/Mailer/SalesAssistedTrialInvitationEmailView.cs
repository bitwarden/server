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

    // Distinct from TrialLength: this is the token lifetime from GlobalSettings, not the trial period.
    public required int ExpiryDays { get; set; }

    public string HeroTitle => TrialLength > 0
        ? $"You're invited to start a <b>{TrialLength}-day free trial</b> of {ProductName}"
        : $"You're invited to try <b>{ProductName}</b>";

    public string ProductName => ProductTier switch
    {
        ProductTierType.Families => "Bitwarden Families",
        ProductTierType.Teams => "Bitwarden Teams",
        ProductTierType.TeamsStarter => "Bitwarden Teams Starter",
        ProductTierType.Enterprise => "Bitwarden Enterprise",
        _ => "Bitwarden"
    };

    public string SpotImageUrl => ProductTier switch
    {
        ProductTierType.Families => "https://assets.bitwarden.com/email/v1/spot-family-homes.png",
        _ => "https://assets.bitwarden.com/email/v1/spot-enterprise.png"
    };

    public IEnumerable<string> Features => ProductTier switch
    {
        ProductTierType.Families =>
        [
            "Securely store and share passwords, credentials, and sensitive data",
            "Cover up to 6 family members, each with their own personal encrypted vault",
            "Store up to 5GB of encrypted file attachments",
        ],
        _ =>
        [
            "Securely store and share passwords, credentials, and sensitive data",
            "Manage team access with group-based permissions and admin controls",
            "Connect to your directory service for automated user provisioning",
        ]
    };

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
