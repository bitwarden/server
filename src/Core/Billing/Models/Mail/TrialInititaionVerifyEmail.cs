using Bit.Core.Auth.Models.Mail;
using Bit.Core.Billing.Enums;

namespace Bit.Core.Billing.Models.Mail;

public class TrialInitiationVerifyEmail : RegisterVerifyEmail
{
    /// <summary>
    /// See comment on <see cref="RegisterVerifyEmail"/>.<see cref="RegisterVerifyEmail.Url"/>
    /// </summary>
    public new string Url
    {
        get =>
            $"{WebVaultUrl}/{Route}"
            + $"?token={Token}"
            + $"&email={Email}"
            + $"&fromEmail=true"
            + $"&productTier={(int)ProductTier}"
            + $"&product={string.Join(",", Product.Select(p => (int)p))}";
    }

    public ProductTierType ProductTier { get; set; }

    public IEnumerable<ProductType> Product { get; set; }

    /// <summary>
    /// Currently we only support one product type at a time, despite Product being a collection.
    /// If we receive both PasswordManager and SecretsManager, we'll send the user to the PM trial route
    /// </summary>
    private string Route =>
        Product.Any(p => p == ProductType.PasswordManager)
            ? "trial-initiation"
            : "secrets-manager-trial-initiation";
}
