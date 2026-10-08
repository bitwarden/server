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
    /// The trial's products, used to select the sign-up route. Supported combinations are Password
    /// Manager, Password Manager + Secrets Manager, and Password Manager + Privileged Controls.
    /// </summary>
    public required IEnumerable<ProductType> Products { get; set; }

    public required int TrialLength { get; set; }

    public required string SenderEmail { get; set; }

    // Distinct from TrialLength: this is the token lifetime from GlobalSettings, not the trial period.
    public required int ExpiryDays { get; set; }

    public string HeroTitle => ProductTier == ProductTierType.Free
    ? "You're invited to try Bitwarden"
    : $"You're invited to start a <b>{TrialLength}-day<br/>free trial</b> of {ProductName}";

    public string ProductName => ProductTier switch
    {
        ProductTierType.Free => "Bitwarden",
        ProductTierType.Families => "Bitwarden Families",
        ProductTierType.Teams => "Bitwarden Teams",
        ProductTierType.Enterprise => "Bitwarden Enterprise",
        _ => throw new InvalidOperationException($"Unexpected ProductTierType: {ProductTier}")
    };

    public string SpotImageUrl => ProductTier switch
    {
        ProductTierType.Free => "https://assets.bitwarden.com/email/v1/account-fill.png",
        ProductTierType.Families => "https://assets.bitwarden.com/email/v1/spot-family-homes.png",
        ProductTierType.Teams => "https://assets.bitwarden.com/email/v1/spot-enterprise.png",
        ProductTierType.Enterprise => "https://assets.bitwarden.com/email/v1/spot-enterprise.png",
        _ => throw new InvalidOperationException($"Unexpected ProductTierType: {ProductTier}")
    };

    public IEnumerable<string> Features => ProductTier switch
    {
        ProductTierType.Free => [],
        ProductTierType.Families =>
        [
            "Securely store and share passwords, credentials, and sensitive data",
            "Cover up to 6 family members, each with their own personal encrypted vault",
            "Store up to 5GB of encrypted file attachments",
        ],
        ProductTierType.Teams =>
        [
            "Securely store and share passwords, credentials, and sensitive data",
            "Manage team access with group-based permissions and admin controls",
            "Connect to your directory service for automated user provisioning",
        ],
        ProductTierType.Enterprise =>
        [
            "Securely store and share passwords, credentials, and sensitive data",
            "Enforce security policies across your entire organization",
            "Integrate with your existing SSO provider and directory services",
        ],
        _ => throw new InvalidOperationException($"Unexpected ProductTierType: {ProductTier}")
    };

    /// <summary>
    /// The destination URL for the invitation CTA. Mirrors the new-user routing in
    /// <see cref="Bit.Core.Billing.Models.Mail.TrialInitiationVerifyEmail"/>:
    /// <list type="bullet">
    /// <item>PM + Privileged Controls trial → <c>privileged-controls-trial-initiation</c>;</item>
    /// <item>PM or PM + SM trial → <c>trial-initiation</c>;</item>
    /// <item>SM-only trial → <c>secrets-manager-trial-initiation</c>.</item>
    /// </list>
    /// Unlike the legacy <c>HandlebarsMailService</c> flow, the IMailer pattern has no service layer to
    /// URL-encode in, so <see cref="Token"/> and <see cref="Email"/> are encoded here. Failing to encode
    /// silently breaks registration when a token contains <c>+</c> or <c>=</c> characters.
    /// </summary>
    public string Url => $"{_globalSettings.BaseServiceUri.VaultWithHash}/{Route}" +
                      $"?productTier={(int)ProductTier}" +
                      $"&product={string.Join(",", Products.Select(p => (int)p))}" +
                      $"&trialLength={TrialLength}" +
                      $"&salesAssistedToken={WebUtility.UrlEncode(Token)}" +
                      $"&email={WebUtility.UrlEncode(Email)}" +
                      "&paymentOptional=true&fromEmail=true";

    private string Route
    {
        get
        {
            var hasPasswordManager = Products.Contains(ProductType.PasswordManager);

            return hasPasswordManager switch
            {
                true when Products.Contains(ProductType.PrivilegedControls) => "privileged-controls-trial-initiation",
                // Password Manager only or Password Manager + Secrets Manager
                true => "trial-initiation",
                // Secrets Manager only.
                _ => "secrets-manager-trial-initiation"
            };
        }
    }
}

public class SalesAssistedTrialInvitationEmail : BaseMail<SalesAssistedTrialInvitationEmailView>
{
    public override string Subject { get; set; } = "You're invited to try Bitwarden";
}
