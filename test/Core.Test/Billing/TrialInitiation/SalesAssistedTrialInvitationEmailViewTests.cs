using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.Mail.Mailer;
using Bit.Core.Settings;
using Xunit;

namespace Bit.Core.Test.Billing.TrialInitiation;

public class SalesAssistedTrialInvitationEmailViewTests
{
    private static SalesAssistedTrialInvitationEmailView CreateView(
        ProductTierType productTier,
        IEnumerable<ProductType>? products = null) =>
        new(new GlobalSettings())
        {
            Token = "token",
            Email = "prospect@example.com",
            ProductTier = productTier,
            Products = products ?? [ProductType.PasswordManager],
            TrialLength = 7,
            SenderEmail = "sales@bitwarden.com",
            ExpiryDays = 5,
        };

    [Fact]
    public void Features_Free_ReturnsEmpty()
    {
        var view = CreateView(ProductTierType.Free);

        Assert.Empty(view.Features);
    }

    [Fact]
    public void Features_Families_ReturnsFamiliesCopy()
    {
        var view = CreateView(ProductTierType.Families);

        Assert.Equal(
            [
                "Securely store and share passwords, credentials, and sensitive data",
                "Cover up to 6 family members, each with their own personal encrypted vault",
                "Store up to 5GB of encrypted file attachments",
            ],
            view.Features);
    }

    [Fact]
    public void Features_Teams_ReturnsTeamsCopy()
    {
        var view = CreateView(ProductTierType.Teams);

        Assert.Equal(
            [
                "Securely store and share passwords, credentials, and sensitive data",
                "Manage team access with group-based permissions and admin controls",
                "Connect to your directory service for automated user provisioning",
            ],
            view.Features);
    }

    [Fact]
    public void Features_Enterprise_ReturnsEnterpriseCopy()
    {
        var view = CreateView(ProductTierType.Enterprise);

        Assert.Equal(
            [
                "Securely store and share passwords, credentials, and sensitive data",
                "Enforce security policies across your entire organization",
                "Integrate with your existing SSO provider and directory services",
            ],
            view.Features);
    }

    [Theory]
    [InlineData(ProductTierType.Families, "https://assets.bitwarden.com/email/v1/spot-family-homes.png")]
    [InlineData(ProductTierType.Free, "https://assets.bitwarden.com/email/v1/account-fill.png")]
    [InlineData(ProductTierType.Teams, "https://assets.bitwarden.com/email/v1/spot-enterprise.png")]
    [InlineData(ProductTierType.Enterprise, "https://assets.bitwarden.com/email/v1/spot-enterprise.png")]
    public void SpotImageUrl_ReturnsTierBucketedImage(ProductTierType productTier, string expectedUrl)
    {
        var view = CreateView(productTier);

        Assert.Equal(expectedUrl, view.SpotImageUrl);
    }

    [Theory]
    [InlineData(ProductTierType.Free, "You're invited to try Bitwarden")]
    [InlineData(ProductTierType.Enterprise, "You're invited to start a <b>7-day<br/>free trial</b> of Bitwarden Enterprise")]
    public void HeroTitle_OmitsTrialLengthForFreeTier(ProductTierType productTier, string expected)
    {
        var view = CreateView(productTier);

        Assert.Equal(expected, view.HeroTitle);
    }

    [Fact]
    public void Url_PasswordManager_UsesTrialInitiationRoute()
    {
        var view = CreateView(ProductTierType.Enterprise, [ProductType.PasswordManager]);

        Assert.Contains("/trial-initiation?", view.Url);
    }

    [Fact]
    public void Url_SecretsManagerOnly_UsesSecretsManagerRoute()
    {
        var view = CreateView(ProductTierType.Enterprise, [ProductType.SecretsManager]);

        Assert.Contains("/secrets-manager-trial-initiation?", view.Url);
    }

    [Fact]
    public void Url_PasswordManagerAndPrivilegedControls_UsesPrivilegedControlsRoute()
    {
        var view = CreateView(ProductTierType.Enterprise, [ProductType.PasswordManager, ProductType.PrivilegedControls]);

        Assert.Contains("/privileged-controls-trial-initiation?", view.Url);
    }

    [Fact]
    public void Url_PasswordManagerAndPrivilegedControls_SerializesBothProductsAsInts()
    {
        var view = CreateView(ProductTierType.Enterprise, [ProductType.PasswordManager, ProductType.PrivilegedControls]);

        Assert.Contains("&product=0,2", view.Url);
    }
}
