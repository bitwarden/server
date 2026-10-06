using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.Mail;
using Xunit;

namespace Bit.Core.Test.Billing.Models.Mail;

public class TrialInitiationVerifyEmailTests
{
    private static TrialInitiationVerifyEmail Model(
        IEnumerable<ProductType> products,
        bool isExistingUser = false,
        bool paymentOptional = false) =>
        new()
        {
            WebVaultUrl = "https://vault.bitwarden.com/#",
            Token = "token",
            Email = "test@example.com",
            ProductTier = ProductTierType.Enterprise,
            Product = products,
            TrialLength = 7,
            IsExistingUser = isExistingUser,
            PaymentOptional = paymentOptional
        };

    [Fact]
    public void Url_PasswordManager_UsesTrialInitiationRoute()
    {
        Assert.Contains("/trial-initiation?", Model([ProductType.PasswordManager]).Url);
    }

    [Fact]
    public void Url_PasswordManagerAndSecretsManager_UsesTrialInitiationRoute()
    {
        var url = Model([ProductType.PasswordManager, ProductType.SecretsManager]).Url;
        Assert.Contains("/trial-initiation?", url);
    }

    [Fact]
    public void Url_SecretsManagerOnly_UsesSecretsManagerRoute()
    {
        Assert.Contains("/secrets-manager-trial-initiation?", Model([ProductType.SecretsManager]).Url);
    }

    [Fact]
    public void Url_PrivilegedControls_UsesPrivilegedControlsRoute()
    {
        var url = Model([ProductType.PasswordManager, ProductType.PrivilegedControls]).Url;
        Assert.Contains("/privileged-controls-trial-initiation?", url);
    }

    [Fact]
    public void Url_ExistingUser_UsesCreateOrganizationRoute()
    {
        var url = Model([ProductType.PasswordManager, ProductType.PrivilegedControls], isExistingUser: true).Url;
        Assert.Contains("/create-organization?", url);
    }

    [Fact]
    public void Url_SerializesProductsAndTierAsInts()
    {
        var url = Model([ProductType.PasswordManager, ProductType.PrivilegedControls]).Url;
        Assert.Contains("&product=0,2", url);
        Assert.Contains("&productTier=3", url);
    }

    [Fact]
    public void Url_PaymentOptional_IncludesParam()
    {
        var url = Model([ProductType.PasswordManager], paymentOptional: true).Url;
        Assert.Contains("&paymentOptional=true", url);
    }

    [Fact]
    public void Url_WithoutPaymentOptional_OmitsParam()
    {
        Assert.DoesNotContain("paymentOptional", Model([ProductType.PasswordManager]).Url);
    }
}
