using Bit.Core.Auth.Models.Mail;
using Xunit;

namespace Bit.Core.Test.Auth.Models.Mail;

public class RegisterVerifyEmailTests
{
    private static RegisterVerifyEmail BuildBaseModel()
    {
        return new RegisterVerifyEmail
        {
            WebVaultUrl = "https://vault.example.com",
            SiteName = "TestSite",
            Token = "test-token",
            Email = "test%40example.com", // pre-URL-encoded per the mail service
        };
    }

    [Fact]
    public void Url_WithoutSealedOpenOrgInviteData_MatchesTodaysBaseline()
    {
        // Regression guard for the standard (non-open-invite) registration flow.
        var model = BuildBaseModel();

        var expected =
            "https://vault.example.com/redirect-connector.html#finish-signup?token=test-token&email=test%40example.com&fromEmail=true";
        Assert.Equal(expected, model.Url);
        Assert.DoesNotContain("sealedOpenOrgInviteData", model.Url);
    }

    [Fact]
    public void Url_WithEmptySealedOpenOrgInviteData_MatchesTodaysBaseline()
    {
        var model = BuildBaseModel();
        model.SealedOpenOrgInviteData = string.Empty;

        Assert.DoesNotContain("sealedOpenOrgInviteData", model.Url);
    }

    [Fact]
    public void Url_WithSealedOpenOrgInviteData_AppendsAfterExistingParams()
    {
        var model = BuildBaseModel();
        model.SealedOpenOrgInviteData = "opaque-base64url-blob";

        var url = model.Url;

        // The passthrough must sit inside the fragment-scoped query, after the existing
        // fromEmail arg, so proxies and access logs never see it (matching the same
        // server-blindness argument that keeps the inviteKey in the fragment).
        var expected =
            "https://vault.example.com/redirect-connector.html#finish-signup?token=test-token&email=test%40example.com&fromEmail=true&sealedOpenOrgInviteData=opaque-base64url-blob";
        Assert.Equal(expected, url);
    }

    [Fact]
    public void Url_WithFromMarketingAndSealedOpenOrgInviteData_AppendsSealedDataLast()
    {
        var model = BuildBaseModel();
        model.FromMarketing = "Premium";
        model.SealedOpenOrgInviteData = "opaque-base64url-blob";

        var url = model.Url;

        // Order: token → email → fromEmail → fromMarketing → sealedOpenOrgInviteData. Order is
        // load-bearing only for future client parsing consistency; both are URL query params so
        // any correct parser can read them regardless of position.
        var expected =
            "https://vault.example.com/redirect-connector.html#finish-signup?token=test-token&email=test%40example.com&fromEmail=true&fromMarketing=Premium&sealedOpenOrgInviteData=opaque-base64url-blob";
        Assert.Equal(expected, url);
    }
}
