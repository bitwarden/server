using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using static Bit.Identity.IntegrationTest.Helpers.RegistrationTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.UserTestHelpers;

namespace Bit.Identity.IntegrationTest.Registration;

public class RegisterSendVerificationEmailTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public RegisterSendVerificationEmailTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [BitAutoData("invalidEmail")]
    [BitAutoData("")]
    public async Task PostRegisterSendEmailVerification_InvalidRequestModel_ThrowsBadRequestException(string email, string name, bool receiveMarketingEmails)
    {

        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails
        };

        var context = await _factory.PostRegisterSendEmailVerificationAsync(model);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task PostRegisterSendEmailVerification_DisabledOpenRegistration_ThrowsBadRequestException(string name, bool receiveMarketingEmails)
    {

        // Localize substitutions to this test.
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:disableUserRegistration", "true");

        var email = $"test+register+{name}@email.com";
        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails
        };

        var context = await localFactory.PostRegisterSendEmailVerificationAsync(model);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task PostRegisterSendEmailVerification_WhenGivenNewOrExistingUser__WithEnableEmailVerificationTrue_ReturnsNoContent(bool shouldPreCreateUser, string name, bool receiveMarketingEmails)
    {
        var email = $"test+register+{name}@email.com";
        if (shouldPreCreateUser)
        {
            await CreateUserAsync(email, name, _factory);
        }

        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails
        };

        var context = await _factory.PostRegisterSendEmailVerificationAsync(model);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task PostRegisterSendEmailVerification_WhenGivenNewOrExistingUser_WithEnableEmailVerificationFalse_ReturnsNoContent(bool shouldPreCreateUser, string name, bool receiveMarketingEmails)
    {

        // Localize substitutions to this test.
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:enableEmailVerification", "false");

        var email = $"test+register+{name}@email.com";
        if (shouldPreCreateUser)
        {
            await CreateUserAsync(email, name, localFactory);
        }

        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails
        };

        var context = await localFactory.PostRegisterSendEmailVerificationAsync(model);

        if (shouldPreCreateUser)
        {
            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            var body = await context.ReadBodyAsStringAsync();
            Assert.Contains($"Email {email} is already taken", body);
        }
        else
        {
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            var body = await context.ReadBodyAsStringAsync();
            Assert.NotNull(body);
            Assert.StartsWith("BwRegistrationEmailVerificationToken_", body);
        }
    }

    [Theory, BitAutoData]
    public async Task PostRegisterSendEmailVerification_WithOpenOrgInvite_InvalidLink_ReturnsBadRequest(string name, bool receiveMarketingEmails)
    {
        // OpenOrgInvite payload without a matching invite link on the org is rejected.
        var localFactory = new IdentityApplicationFactory();

        var email = $"test+register+badlink+{name}@email.com";

        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails,
            OpenOrgInvite = new RegisterStartOpenOrgInviteRequestModel
            {
                OrganizationId = Guid.NewGuid(),
                Code = Guid.NewGuid(),
                SealedOpenOrgInviteData = "opaque-base64url-blob",
            },
        };

        var context = await localFactory.PostRegisterSendEmailVerificationAsync(model);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task PostRegisterSendEmailVerification_WithOpenOrgInvite_OversizedSealedData_ReturnsBadRequest(string name, bool receiveMarketingEmails)
    {
        var email = $"test+register+oversize+{name}@email.com";
        // Length cap in the request model is 4096; 4097+ must be rejected by model validation.
        var oversized = new string('A', 4097);

        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails,
            OpenOrgInvite = new RegisterStartOpenOrgInviteRequestModel
            {
                OrganizationId = Guid.NewGuid(),
                Code = Guid.NewGuid(),
                SealedOpenOrgInviteData = oversized,
            },
        };

        var context = await _factory.PostRegisterSendEmailVerificationAsync(model);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task PostRegisterSendEmailVerification_WithMatchingOrgInvite_BypassesClaimedDomainBlock(string name, bool receiveMarketingEmails)
    {
        // Isolated factory to keep the seeded org/policy/domain out of the shared fixture.
        var localFactory = new IdentityApplicationFactory();

        var claimedDomain = $"claimed-{Guid.NewGuid():N}.example.com";
        var email = $"test+claimed+{name}@{claimedDomain}";
        var (_, inviteLink) = await SeedOrgWithClaimedDomainAndInviteLinkAsync(localFactory, claimedDomain);

        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails,
            OpenOrgInvite = new RegisterStartOpenOrgInviteRequestModel
            {
                OrganizationId = inviteLink.OrganizationId,
                Code = Guid.Parse(inviteLink.Code),
                SealedOpenOrgInviteData = "opaque-base64url-blob",
            },
        };

        var context = await localFactory.PostRegisterSendEmailVerificationAsync(model);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task PostRegisterSendEmailVerification_WithDifferentOrgInvite_StillBlocksClaimedDomain(string name, bool receiveMarketingEmails)
    {
        // Attacker scenario: sender's invite belongs to OrgB, but the email's domain is claimed by OrgA.
        // OrgA's block policy must still fire because the exclusion is scoped to OrgB, not OrgA.
        var localFactory = new IdentityApplicationFactory();

        var claimedDomain = $"claimed-{Guid.NewGuid():N}.example.com";
        var email = $"test+attacker+{name}@{claimedDomain}";
        await SeedOrgWithClaimedDomainAndInviteLinkAsync(localFactory, claimedDomain);
        // OrgB admits the attacker's email so the 400 must come from OrgA's block policy, not
        // OrgB's own AllowedDomains — keeps this test focused on the exclusion-scoping guarantee.
        var (_, attackerInviteLink) = await SeedOrgWithInviteLinkAsync(
            localFactory, allowedDomains: new[] { claimedDomain });

        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails,
            OpenOrgInvite = new RegisterStartOpenOrgInviteRequestModel
            {
                OrganizationId = attackerInviteLink.OrganizationId,
                Code = Guid.Parse(attackerInviteLink.Code),
                SealedOpenOrgInviteData = "opaque-base64url-blob",
            },
        };

        var context = await localFactory.PostRegisterSendEmailVerificationAsync(model);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task PostRegisterSendEmailVerification_WithOpenOrgInvite_EmailDomainNotInAllowedDomains_ReturnsBadRequest(string name, bool receiveMarketingEmails)
    {
        // The registering email's domain is claimed by OrgA, but OrgA's invite link permits a
        // different domain. Possession of the {orgId, code} alone must NOT grant the domain-block
        // exclusion — the invite link would reject this email at accept time, so the exclusion
        // must gate on the link's AllowedDomains as well.
        var localFactory = new IdentityApplicationFactory();

        var claimedDomain = $"claimed-{Guid.NewGuid():N}.example.com";
        var permittedDomain = $"partner-{Guid.NewGuid():N}.example.com";
        var email = $"test+claimednotallowed+{name}@{claimedDomain}";
        var (_, inviteLink) = await SeedOrgWithClaimedDomainAndInviteLinkAsync(
            localFactory, claimedDomain, allowedDomains: new[] { permittedDomain });

        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails,
            OpenOrgInvite = new RegisterStartOpenOrgInviteRequestModel
            {
                OrganizationId = inviteLink.OrganizationId,
                Code = Guid.Parse(inviteLink.Code),
                SealedOpenOrgInviteData = "opaque-base64url-blob",
            },
        };

        var context = await localFactory.PostRegisterSendEmailVerificationAsync(model);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }
}
