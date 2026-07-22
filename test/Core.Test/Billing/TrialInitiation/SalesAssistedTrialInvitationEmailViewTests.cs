using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.Mail.Mailer;
using Bit.Core.Settings;
using Xunit;

namespace Bit.Core.Test.Billing.TrialInitiation;

public class SalesAssistedTrialInvitationEmailViewTests
{
    private static SalesAssistedTrialInvitationEmailView CreateView(ProductTierType productTier) =>
        new(new GlobalSettings())
        {
            Token = "token",
            Email = "prospect@example.com",
            ProductTier = productTier,
            Products = [ProductType.PasswordManager],
            TrialLength = 7,
            PaymentOptional = false,
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
}
