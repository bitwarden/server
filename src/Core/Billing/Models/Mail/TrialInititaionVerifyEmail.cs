// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

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
        get => $"{WebVaultUrl}/{Route}" +
               $"?token={Token}" +
               $"&email={Email}" +
               $"&fromEmail=true" +
               $"&productTier={(int)ProductTier}" +
               $"&product={string.Join(",", Product.Select(p => (int)p))}" +
               $"&trialLength={TrialLength}";
    }

    public string VerifyYourEmailHTMLCopy =>
        TrialLength == 7
            ? "Verify your email address below to finish signing up for your free trial."
            : $"Verify your email address below to finish signing up for your {ProductTier.GetDisplayName()} plan.";

    public string VerifyYourEmailTextCopy =>
        TrialLength == 7
            ? "Verify your email address using the link below and start your free trial of Bitwarden."
            : $"Verify your email address using the link below and start your {ProductTier.GetDisplayName()} Bitwarden plan.";

    public ProductTierType ProductTier { get; set; }

    public IEnumerable<ProductType> Product { get; set; }

    public int TrialLength { get; set; }

    /// <summary>
    /// Currently we only support one product type at a time, despite Product being a collection.
    /// If we receive both PasswordManager and SecretsManager, we'll send the user to the PM trial route
    /// </summary>
    private string Route
    {
        get
        {
            if (IsExistingUser)
            {
                return "create-organization";
            }

            return Product.Any(p => p == ProductType.PasswordManager)
                ? "trial-initiation"
                : "secrets-manager-trial-initiation";
        }
    }
}
