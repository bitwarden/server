using Bit.Core.Auth.Models.Mail;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;

namespace Bit.Core.Billing.Models.Mail;

public class TrialInitiationVerifyEmail : RegisterVerifyEmail
{
    public bool IsExistingUser { get; set; }
    /// <summary>
    /// See comment on <see cref="RegisterVerifyEmail"/>.<see cref="RegisterVerifyEmail.Url"/>
    /// </summary>
    public new string Url
    {
        get
        {
            var url = $"{WebVaultUrl}/{Route}" +
                      $"?token={Token}" +
                      $"&email={Email}" +
                      $"&fromEmail=true" +
                      $"&productTier={(int)ProductTier}" +
                      $"&product={string.Join(",", Product.Select(p => (int)p))}" +
                      $"&trialLength={TrialLength}";

            if (PaymentOptional)
            {
                url += "&paymentOptional=true";
            }

            if (PamSeatMinimum.HasValue)
            {
                url += $"&pamSeatMinimum={PamSeatMinimum}";
            }

            return url;
        }
    }

    public string VerifyYourEmailHTMLCopy =>
        TrialLength > 0
            ? "Verify your email address below to finish signing up for your free trial."
            : $"Verify your email address below to finish signing up for your {ProductTier.GetDisplayName()} plan.";

    public string VerifyYourEmailTextCopy =>
        TrialLength > 0
            ? "Verify your email address using the link below and start your free trial of Bitwarden."
            : $"Verify your email address using the link below and start your {ProductTier.GetDisplayName()} Bitwarden plan.";

    public ProductTierType ProductTier { get; set; }

    public IEnumerable<ProductType> Product { get; set; } = null!;

    public int TrialLength { get; set; }

    public bool PaymentOptional { get; set; }

    public int? PamSeatMinimum { get; set; }

    /// <summary>
    /// Selects the sign-up route from the trial's products. Supported combinations are Password Manager,
    /// Password Manager + Secrets Manager, and Password Manager + Privileged Controls.
    /// </summary>
    private string Route
    {
        get
        {
            if (IsExistingUser)
            {
                return "create-organization";
            }

            var hasPasswordManager = Product.Contains(ProductType.PasswordManager);

            return hasPasswordManager switch
            {
                true when Product.Contains(ProductType.PrivilegedControls) => "privileged-controls-trial-initiation",
                // Password Manager only or Password Manager + Secrets Manager
                true => "trial-initiation",
                // Secrets Manager only.
                _ => "secrets-manager-trial-initiation"
            };

        }
    }
}
