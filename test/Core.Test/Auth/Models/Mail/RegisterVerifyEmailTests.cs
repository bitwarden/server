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

        var expected =
            "https://vault.example.com/redirect-connector.html#finish-signup?token=test-token&email=test%40example.com&fromEmail=true&fromMarketing=Premium&sealedOpenOrgInviteData=opaque-base64url-blob";
        Assert.Equal(expected, url);
    }
}
