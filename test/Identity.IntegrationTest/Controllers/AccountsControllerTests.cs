using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.Repositories;
using Bit.Core.Tokens;
using Bit.Core.Utilities;
using Bit.Identity.Models.Request.Accounts;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Bit.Identity.IntegrationTest.Controllers;

public class AccountsControllerTests : IClassFixture<IdentityApplicationFactory>
{
    private const string GenerateInviteLinkFlagSettingKey =
        $"globalSettings:launchDarkly:flagValues:{FeatureFlagKeys.GenerateInviteLink}";

    private readonly IdentityApplicationFactory _factory;

    public AccountsControllerTests(IdentityApplicationFactory factory)
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
            await CreateUserAsync(email, name);
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

    [Theory, BitAutoData]
    public async Task PostRegisterSendEmailVerification_WithOpenOrgInvite_InvalidLink_ReturnsBadRequest(string name, bool receiveMarketingEmails)
    {
        // OpenOrgInvite payload without a matching invite link on the org is rejected.
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration(GenerateInviteLinkFlagSettingKey, "true");

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
    public async Task PostRegisterSendEmailVerification_WithOpenOrgInviteAndFeatureFlagOff_ReturnsNotFound(string name, bool receiveMarketingEmails)
    {
        // With the flag turned off, the endpoint must refuse to honor the OpenOrgInvite payload
        // — mirroring [RequireFeature] on the sibling invite-link surfaces (→ 404). The other
        // invite-link OpenOrgInvite tests in this file explicitly turn the flag ON; this one is
        // the sole flag-OFF integration case.
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration(GenerateInviteLinkFlagSettingKey, "false");

        var model = new RegisterSendVerificationEmailRequestModel
        {
            Email = $"test+flagoff+{name}@example.com",
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

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task PostRegisterSendEmailVerification_WithMatchingOrgInvite_BypassesClaimedDomainBlock(string name, bool receiveMarketingEmails)
    {
        // Isolated factory to keep the seeded org/policy/domain out of the shared fixture.
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration(GenerateInviteLinkFlagSettingKey, "true");

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
        localFactory.UpdateConfiguration(GenerateInviteLinkFlagSettingKey, "true");

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
        localFactory.UpdateConfiguration(GenerateInviteLinkFlagSettingKey, "true");

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

    private static async Task<(Organization Org, OrganizationInviteLink InviteLink)> SeedOrgWithClaimedDomainAndInviteLinkAsync(
        IdentityApplicationFactory factory, string claimedDomain, IEnumerable<string>? allowedDomains = null)
    {
        var organizationRepository = factory.Services.GetRequiredService<IOrganizationRepository>();
        var organizationDomainRepository = factory.Services.GetRequiredService<IOrganizationDomainRepository>();
        var policyRepository = factory.Services.GetRequiredService<IPolicyRepository>();
        var organizationInviteLinkRepository = factory.Services.GetRequiredService<IOrganizationInviteLinkRepository>();

        var organization = new Organization
        {
            Name = $"ClaimedDomainOrg-{Guid.NewGuid():N}",
            BillingEmail = $"billing+{Guid.NewGuid():N}@example.com",
            Plan = "Enterprise",
            Enabled = true,
            UsePolicies = true,
            UseOrganizationDomains = true,
            UseInviteLinks = true,
        };
        organization = await organizationRepository.CreateAsync(organization);

        var domain = new OrganizationDomain
        {
            OrganizationId = organization.Id,
            DomainName = claimedDomain,
            Txt = "bw-test",
        };
        domain.SetVerifiedDate();
        await organizationDomainRepository.CreateAsync(domain);

        var policy = new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.BlockClaimedDomainAccountCreation,
            Enabled = true,
        };
        await policyRepository.CreateAsync(policy);

        var inviteLink = new OrganizationInviteLink
        {
            OrganizationId = organization.Id,
            Invite = "opaque-invite-blob",
            SupportsConfirmation = false,
        };
        inviteLink.SetAllowedDomains(allowedDomains ?? new[] { claimedDomain });
        inviteLink.SetNewId();
        inviteLink.SetNewCode();
        await organizationInviteLinkRepository.CreateAsync(inviteLink);

        return (organization, inviteLink);
    }

    private static async Task<(Organization Org, OrganizationInviteLink InviteLink)> SeedOrgWithInviteLinkAndTwoFactorPolicyAsync(
        IdentityApplicationFactory factory, IEnumerable<string>? allowedDomains = null)
    {
        var organizationRepository = factory.Services.GetRequiredService<IOrganizationRepository>();
        var policyRepository = factory.Services.GetRequiredService<IPolicyRepository>();
        var organizationInviteLinkRepository = factory.Services.GetRequiredService<IOrganizationInviteLinkRepository>();

        var organization = new Organization
        {
            Name = $"TwoFactorOrg-{Guid.NewGuid():N}",
            BillingEmail = $"billing+{Guid.NewGuid():N}@example.com",
            Plan = "Enterprise",
            Enabled = true,
            UsePolicies = true,
            UseInviteLinks = true,
        };
        organization = await organizationRepository.CreateAsync(organization);

        var twoFactorPolicy = new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.TwoFactorAuthentication,
            Enabled = true,
        };
        await policyRepository.CreateAsync(twoFactorPolicy);

        var inviteLink = new OrganizationInviteLink
        {
            OrganizationId = organization.Id,
            Invite = "opaque-invite-blob",
            SupportsConfirmation = false,
        };
        inviteLink.SetAllowedDomains(allowedDomains ?? new[] { "email.com" });
        inviteLink.SetNewId();
        inviteLink.SetNewCode();
        await organizationInviteLinkRepository.CreateAsync(inviteLink);

        return (organization, inviteLink);
    }

    private static async Task<(Organization Org, OrganizationInviteLink InviteLink)> SeedOrgWithClaimedDomainAndTwoFactorPolicyAndInviteLinkAsync(
        IdentityApplicationFactory factory, string claimedDomain, IEnumerable<string>? allowedDomains = null)
    {
        var organizationRepository = factory.Services.GetRequiredService<IOrganizationRepository>();
        var organizationDomainRepository = factory.Services.GetRequiredService<IOrganizationDomainRepository>();
        var policyRepository = factory.Services.GetRequiredService<IPolicyRepository>();
        var organizationInviteLinkRepository = factory.Services.GetRequiredService<IOrganizationInviteLinkRepository>();

        var organization = new Organization
        {
            Name = $"ClaimedDomain2FaOrg-{Guid.NewGuid():N}",
            BillingEmail = $"billing+{Guid.NewGuid():N}@example.com",
            Plan = "Enterprise",
            Enabled = true,
            UsePolicies = true,
            UseOrganizationDomains = true,
            UseInviteLinks = true,
        };
        organization = await organizationRepository.CreateAsync(organization);

        var domain = new OrganizationDomain
        {
            OrganizationId = organization.Id,
            DomainName = claimedDomain,
            Txt = "bw-test",
        };
        domain.SetVerifiedDate();
        await organizationDomainRepository.CreateAsync(domain);

        var domainBlockPolicy = new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.BlockClaimedDomainAccountCreation,
            Enabled = true,
        };
        await policyRepository.CreateAsync(domainBlockPolicy);

        var twoFactorPolicy = new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.TwoFactorAuthentication,
            Enabled = true,
        };
        await policyRepository.CreateAsync(twoFactorPolicy);

        var inviteLink = new OrganizationInviteLink
        {
            OrganizationId = organization.Id,
            Invite = "opaque-invite-blob",
            SupportsConfirmation = false,
        };
        inviteLink.SetAllowedDomains(allowedDomains ?? new[] { claimedDomain });
        inviteLink.SetNewId();
        inviteLink.SetNewCode();
        await organizationInviteLinkRepository.CreateAsync(inviteLink);

        return (organization, inviteLink);
    }

    private static async Task<(Organization Org, OrganizationInviteLink InviteLink)> SeedOrgWithInviteLinkAsync(
        IdentityApplicationFactory factory, IEnumerable<string>? allowedDomains = null)
    {
        var organizationRepository = factory.Services.GetRequiredService<IOrganizationRepository>();
        var organizationInviteLinkRepository = factory.Services.GetRequiredService<IOrganizationInviteLinkRepository>();

        var organization = new Organization
        {
            Name = $"OtherOrg-{Guid.NewGuid():N}",
            BillingEmail = $"billing+{Guid.NewGuid():N}@example.com",
            Plan = "Enterprise",
            Enabled = true,
            UsePolicies = true,
            UseInviteLinks = true,
        };
        organization = await organizationRepository.CreateAsync(organization);

        var inviteLink = new OrganizationInviteLink
        {
            OrganizationId = organization.Id,
            Invite = "opaque-invite-blob",
            SupportsConfirmation = false,
        };
        inviteLink.SetAllowedDomains(allowedDomains ?? Array.Empty<string>());
        inviteLink.SetNewId();
        inviteLink.SetNewCode();
        await organizationInviteLinkRepository.CreateAsync(inviteLink);

        return (organization, inviteLink);
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
    // marketing emails can stay at top level
    public async Task RegistrationWithEmailVerification_WithEmailVerificationToken_Succeeds([Required] string name, bool receiveMarketingEmails,
         [StringLength(1000), Required] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, [Required] string userSymmetricKey,
         [Required] KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism)
    {
        userAsymmetricKeys.AccountKeys = null;
        // Localize substitutions to this test.
        var localFactory = new IdentityApplicationFactory();

        // we must first call the send verification email endpoint to trigger the first part of the process
        var email = $"test+register+{name}@email.com";
        var sendVerificationEmailReqModel = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            ReceiveMarketingEmails = receiveMarketingEmails
        };

        var sendEmailVerificationResponseHttpContext = await localFactory.PostRegisterSendEmailVerificationAsync(sendVerificationEmailReqModel);

        Assert.Equal(StatusCodes.Status204NoContent, sendEmailVerificationResponseHttpContext.Response.StatusCode);
        Assert.NotNull(localFactory.RegistrationTokens[email]);

        // Now we call the finish registration endpoint with the email verification token
        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            EmailVerificationToken = localFactory.RegistrationTokens[email],
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, postRegisterFinishHttpContext.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users
            .SingleAsync(u => u.Email == email);

        Assert.NotNull(user);

        // Assert user properties match the request model
        Assert.Equal(email, user.Email);
        Assert.Equal(name, user.Name);
        Assert.NotEqual(masterPasswordHash, user.MasterPassword);  // We execute server side hashing
        Assert.NotNull(user.MasterPassword);
        Assert.Equal(masterPasswordHint, user.MasterPasswordHint);
        Assert.Equal(userSymmetricKey, user.Key);
        Assert.Equal(userAsymmetricKeys.EncryptedPrivateKey, user.PrivateKey);
        Assert.Equal(userAsymmetricKeys.PublicKey, user.PublicKey);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        Assert.Equal(KdfConstants.PBKDF2_ITERATIONS.Default, user.KdfIterations);
        Assert.Equal(kdfMemory, user.KdfMemory);
        Assert.Equal(kdfParallelism, user.KdfParallelism);
    }


    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_OpenRegistrationDisabled_ThrowsBadRequestException([Required] string name, string emailVerificationToken,
       [StringLength(1000), Required] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, [Required] string userSymmetricKey,
       [Required] KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism)
    {
        userAsymmetricKeys.AccountKeys = null;
        // Localize substitutions to this test.
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:disableUserRegistration", "true");

        var email = $"test+register+{name}@email.com";

        // Now we call the finish registration endpoint with the email verification token
        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            EmailVerificationToken = emailVerificationToken,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status400BadRequest, postRegisterFinishHttpContext.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithOpenOrgInviteAndOpenRegistrationDisabled_Succeeds(
        [Required] string name, bool receiveMarketingEmails,
        [StringLength(1000), Required] string masterPasswordHash, [StringLength(50)] string masterPasswordHint,
        [Required] string userSymmetricKey, [Required] KeysRequestModel userAsymmetricKeys,
        int kdfMemory, int kdfParallelism)
    {
        // The DisableUserRegistration self-hosted admin toggle targets open self-registration.
        // Possession of a valid invite link is the authorization for this path, so both
        // register-start and register-finish via an open org invite must proceed even when
        // that toggle is on.
        userAsymmetricKeys.AccountKeys = null;
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:disableUserRegistration", "true");
        localFactory.UpdateConfiguration(GenerateInviteLinkFlagSettingKey, "true");

        var email = $"test+openinvitedisabled+{name}@email.com";
        var (_, inviteLink) = await SeedOrgWithInviteLinkAsync(localFactory, allowedDomains: new[] { "email.com" });

        var sendReqModel = new RegisterSendVerificationEmailRequestModel
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
        var sendCtx = await localFactory.PostRegisterSendEmailVerificationAsync(sendReqModel);
        Assert.Equal(StatusCodes.Status204NoContent, sendCtx.Response.StatusCode);
        Assert.NotNull(localFactory.RegistrationTokens[email]);

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            EmailVerificationToken = localFactory.RegistrationTokens[email],
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism,
            OpenOrgInvite = new OpenOrgInviteRequestModel
            {
                OrganizationId = inviteLink.OrganizationId,
                Code = Guid.Parse(inviteLink.Code),
            },
        };
        var finishCtx = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, finishCtx.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users.SingleAsync(u => u.Email == email);
        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.Equal(name, user.Name);
    }

    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithMatchingOpenOrgInvite_Succeeds([Required] string name, bool receiveMarketingEmails,
        [StringLength(1000), Required] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, [Required] string userSymmetricKey,
        [Required] KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism)
    {
        userAsymmetricKeys.AccountKeys = null;
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration(GenerateInviteLinkFlagSettingKey, "true");

        var claimedDomain = $"claimed-{Guid.NewGuid():N}.example.com";
        var email = $"test+claimedfinish+{name}@{claimedDomain}";
        var (_, inviteLink) = await SeedOrgWithClaimedDomainAndInviteLinkAsync(localFactory, claimedDomain);

        // Register-start with the matching invite — bypasses the claimed-domain block, yields a token.
        var sendReqModel = new RegisterSendVerificationEmailRequestModel
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
        var sendCtx = await localFactory.PostRegisterSendEmailVerificationAsync(sendReqModel);
        Assert.Equal(StatusCodes.Status204NoContent, sendCtx.Response.StatusCode);
        Assert.NotNull(localFactory.RegistrationTokens[email]);

        // Register-finish with the same invite — the domain-block check must exclude this org.
        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            EmailVerificationToken = localFactory.RegistrationTokens[email],
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism,
            OpenOrgInvite = new OpenOrgInviteRequestModel
            {
                OrganizationId = inviteLink.OrganizationId,
                Code = Guid.Parse(inviteLink.Code),
            },
        };
        var finishCtx = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, finishCtx.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users.SingleAsync(u => u.Email == email);
        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.Equal(name, user.Name);

        // Seeded org has no Require 2FA policy — user must not be initialized with 2FA providers.
        Assert.Null(user.GetTwoFactorProviders());
    }

    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithOpenOrgInviteAndTwoFactorPolicyEnabled_SeedsEmail2Fa(
        [Required] string name, bool receiveMarketingEmails,
        [StringLength(1000), Required] string masterPasswordHash, [StringLength(50)] string masterPasswordHint,
        [Required] string userSymmetricKey, [Required] KeysRequestModel userAsymmetricKeys,
        int kdfMemory, int kdfParallelism)
    {
        userAsymmetricKeys.AccountKeys = null;
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration(GenerateInviteLinkFlagSettingKey, "true");

        var email = $"test+2fapolicy+{name}@email.com";
        var (_, inviteLink) = await SeedOrgWithInviteLinkAndTwoFactorPolicyAsync(localFactory);

        var sendReqModel = new RegisterSendVerificationEmailRequestModel
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
        var sendCtx = await localFactory.PostRegisterSendEmailVerificationAsync(sendReqModel);
        Assert.Equal(StatusCodes.Status204NoContent, sendCtx.Response.StatusCode);
        Assert.NotNull(localFactory.RegistrationTokens[email]);

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            EmailVerificationToken = localFactory.RegistrationTokens[email],
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism,
            OpenOrgInvite = new OpenOrgInviteRequestModel
            {
                OrganizationId = inviteLink.OrganizationId,
                Code = Guid.Parse(inviteLink.Code),
            },
        };
        var finishCtx = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, finishCtx.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users.SingleAsync(u => u.Email == email);
        Assert.NotNull(user);

        var providers = user.GetTwoFactorProviders();
        Assert.NotNull(providers);
        Assert.True(providers.TryGetValue(TwoFactorProviderType.Email, out var emailProvider));
        Assert.True(emailProvider!.Enabled);
        Assert.Equal(email.ToLowerInvariant(), emailProvider.MetaData["Email"]?.ToString());
    }

    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithOpenOrgInviteAndClaimedDomainAndTwoFactorPolicy_BypassesDomainAndSeedsEmail2Fa(
        [Required] string name, bool receiveMarketingEmails,
        [StringLength(1000), Required] string masterPasswordHash, [StringLength(50)] string masterPasswordHint,
        [Required] string userSymmetricKey, [Required] KeysRequestModel userAsymmetricKeys,
        int kdfMemory, int kdfParallelism)
    {
        // Exercises the interaction: one org has BOTH a claimed-domain block AND a Require-2FA
        // policy. Registering via that org's open invite must (a) skip the domain block because
        // the invite matches the claiming org and (b) still seed Email 2FA before the user row
        // is persisted. Neither fix in isolation covers this cross-feature path.
        userAsymmetricKeys.AccountKeys = null;
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration(GenerateInviteLinkFlagSettingKey, "true");

        var claimedDomain = $"claimed-{Guid.NewGuid():N}.example.com";
        var email = $"test+bothpolicies+{name}@{claimedDomain}";
        var (_, inviteLink) = await SeedOrgWithClaimedDomainAndTwoFactorPolicyAndInviteLinkAsync(localFactory, claimedDomain);

        // Register-start with the matching invite — bypasses claimed-domain block.
        var sendReqModel = new RegisterSendVerificationEmailRequestModel
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
        var sendCtx = await localFactory.PostRegisterSendEmailVerificationAsync(sendReqModel);
        Assert.Equal(StatusCodes.Status204NoContent, sendCtx.Response.StatusCode);
        Assert.NotNull(localFactory.RegistrationTokens[email]);

        // Register-finish — the domain-block check must exclude this org AND the 2FA policy must fire.
        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            EmailVerificationToken = localFactory.RegistrationTokens[email],
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism,
            OpenOrgInvite = new OpenOrgInviteRequestModel
            {
                OrganizationId = inviteLink.OrganizationId,
                Code = Guid.Parse(inviteLink.Code),
            },
        };
        var finishCtx = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, finishCtx.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users.SingleAsync(u => u.Email == email);
        Assert.NotNull(user);
        Assert.Equal(email, user.Email);

        // Assert both feature paths fired: the domain bypass produced a user row, AND Email 2FA
        // was seeded because the same org has Require-2FA on.
        var providers = user.GetTwoFactorProviders();
        Assert.NotNull(providers);
        Assert.True(providers.TryGetValue(TwoFactorProviderType.Email, out var emailProvider));
        Assert.True(emailProvider!.Enabled);
        Assert.Equal(email.ToLowerInvariant(), emailProvider.MetaData["Email"]?.ToString());
    }

    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithOpenOrgInviteAndEmailDomainNotInAllowedDomains_ReturnsBadRequest(
        [Required] string name, [StringLength(1000), Required] string masterPasswordHash,
        [Required] string userSymmetricKey, [Required] KeysRequestModel userAsymmetricKeys)
    {
        // Mirror of the register-start AllowedDomains gap test at the register-finish endpoint:
        // a bearer of an invite {orgId, code} whose AllowedDomains does not admit the email must
        // not receive the domain-block exclusion when finishing registration either.
        userAsymmetricKeys.AccountKeys = null;
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration(GenerateInviteLinkFlagSettingKey, "true");

        var email = $"test+finishnotallowed+{name}@email.com";

        // Register-start with no invite for an unclaimed email — yields a plain token.
        var sendReqModel = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
        };
        var sendCtx = await localFactory.PostRegisterSendEmailVerificationAsync(sendReqModel);
        Assert.Equal(StatusCodes.Status204NoContent, sendCtx.Response.StatusCode);
        Assert.NotNull(localFactory.RegistrationTokens[email]);

        // Seed an org whose invite permits only a different domain than the email uses.
        var (_, inviteLink) = await SeedOrgWithInviteLinkAsync(
            localFactory, allowedDomains: new[] { "different.example.com" });

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            EmailVerificationToken = localFactory.RegistrationTokens[email],
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            OpenOrgInvite = new OpenOrgInviteRequestModel
            {
                OrganizationId = inviteLink.OrganizationId,
                Code = Guid.Parse(inviteLink.Code),
            },
        };
        var finishCtx = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status400BadRequest, finishCtx.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithInvalidOpenOrgInvite_ReturnsBadRequest([Required] string name,
        [StringLength(1000), Required] string masterPasswordHash, [Required] string userSymmetricKey,
        [Required] KeysRequestModel userAsymmetricKeys)
    {
        userAsymmetricKeys.AccountKeys = null;
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration(GenerateInviteLinkFlagSettingKey, "true");

        var email = $"test+register+badfinishlink+{name}@email.com";

        // Register-start with no invite to obtain a plain email verification token.
        var sendReqModel = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
        };
        var sendCtx = await localFactory.PostRegisterSendEmailVerificationAsync(sendReqModel);
        Assert.Equal(StatusCodes.Status204NoContent, sendCtx.Response.StatusCode);

        // Register-finish carrying a bogus OpenOrgInvite — validator query returns InviteLinkNotFound → 400.
        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            EmailVerificationToken = localFactory.RegistrationTokens[email],
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            OpenOrgInvite = new OpenOrgInviteRequestModel
            {
                OrganizationId = Guid.NewGuid(),
                Code = Guid.NewGuid(),
            },
        };
        var finishCtx = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status400BadRequest, finishCtx.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithDifferentOrgOpenOrgInvite_StillBlocksClaimedDomain([Required] string name,
        [StringLength(1000), Required] string masterPasswordHash, [Required] string userSymmetricKey,
        [Required] KeysRequestModel userAsymmetricKeys)
    {
        // Attacker at register-finish: the token was obtained via OrgA's invite (the legitimate claimant),
        // but the caller now swaps OrgB's invite into the register-finish body hoping to reach past OrgA's
        // block policy. The domain-block check must still exclude only OrgB, so OrgA's policy fires → 400.
        userAsymmetricKeys.AccountKeys = null;
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration(GenerateInviteLinkFlagSettingKey, "true");

        var claimedDomain = $"claimed-{Guid.NewGuid():N}.example.com";
        var email = $"test+attackerfinish+{name}@{claimedDomain}";
        var (_, orgAInvite) = await SeedOrgWithClaimedDomainAndInviteLinkAsync(localFactory, claimedDomain);
        // OrgB admits the attacker's email so the 400 must come from OrgA's block policy, not
        // OrgB's own AllowedDomains — keeps this test focused on the exclusion-scoping guarantee.
        var (_, orgBInvite) = await SeedOrgWithInviteLinkAsync(
            localFactory, allowedDomains: new[] { claimedDomain });

        // Register-start with OrgA's invite so the claimed-domain block is bypassed and we receive a token.
        var sendReqModel = new RegisterSendVerificationEmailRequestModel
        {
            Email = email,
            Name = name,
            OpenOrgInvite = new RegisterStartOpenOrgInviteRequestModel
            {
                OrganizationId = orgAInvite.OrganizationId,
                Code = Guid.Parse(orgAInvite.Code),
                SealedOpenOrgInviteData = "opaque-base64url-blob",
            },
        };
        var sendCtx = await localFactory.PostRegisterSendEmailVerificationAsync(sendReqModel);
        Assert.Equal(StatusCodes.Status204NoContent, sendCtx.Response.StatusCode);
        Assert.NotNull(localFactory.RegistrationTokens[email]);

        // Register-finish swaps to OrgB's invite. Exclusion is scoped to OrgB, OrgA's policy still fires.
        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            EmailVerificationToken = localFactory.RegistrationTokens[email],
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            OpenOrgInvite = new OpenOrgInviteRequestModel
            {
                OrganizationId = orgBInvite.OrganizationId,
                Code = Guid.Parse(orgBInvite.Code),
            },
        };
        var finishCtx = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status400BadRequest, finishCtx.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task RegisterFinish_WithOpenOrgInviteAndOrgInviteToken_ReturnsBadRequest([Required] string name,
        [StringLength(1000), Required] string masterPasswordHash, [Required] string userSymmetricKey,
        [Required] KeysRequestModel userAsymmetricKeys, string orgInviteToken)
    {
        // DTO validation: OpenOrgInvite is only compatible with the EmailVerification token type.
        // Sending it alongside an OrgInviteToken must be rejected at the model-validation layer → 400.
        userAsymmetricKeys.AccountKeys = null;
        var email = $"test+register+dtoreject+{name}@email.com";

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            OrgInviteToken = orgInviteToken,
            OrganizationUserId = Guid.NewGuid(),
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            OpenOrgInvite = new OpenOrgInviteRequestModel
            {
                OrganizationId = Guid.NewGuid(),
                Code = Guid.NewGuid(),
            },
        };

        var finishCtx = await _factory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status400BadRequest, finishCtx.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithOrgInviteToken_Succeeds(
         [StringLength(1000)] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism)
    {
        userAsymmetricKeys.AccountKeys = null;

        // Localize factory to just this test.
        var localFactory = new IdentityApplicationFactory();

        // To avoid having to call the API send org invite endpoint, I'm going to hardcode some valid org invite data:
        var email = "jsnider+local410@bitwarden.com";
        var orgInviteToken = "BwOrgUserInviteToken_CfDJ8HOzu6wr6nVLouuDxgOHsMwPcj9Guuip5k_XLD1bBGpwQS1f66c9kB6X4rvKGxNdywhgimzgvG9SgLwwJU70O8P879XyP94W6kSoT4N25a73kgW3nU3vl3fAtGSS52xdBjNU8o4sxmomRvhOZIQ0jwtVjdMC2IdybTbxwCZhvN0hKIFs265k6wFRSym1eu4NjjZ8pmnMneG0PlKnNZL93tDe8FMcqStJXoddIEgbA99VJp8z1LQmOMfEdoMEM7Zs8W5bZ34N4YEGu8XCrVau59kGtWQk7N4rPV5okzQbTpeoY_4FeywgLFGm-tDtTPEdSEBJkRjexANri7CGdg3dpnMifQc_bTmjZd32gOjw8N8v";
        var orgUserId = new Guid("5e45fbdc-a080-4a77-93ff-b19c0161e81e");

        var orgUser = new OrganizationUser { Id = orgUserId, Email = email, OrganizationId = Guid.NewGuid() };

        var orgInviteTokenable = new OrgUserInviteTokenable(orgUser)
        {
            ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromHours(5))
        };

        localFactory.SubstituteService<IDataProtectorTokenFactory<OrgUserInviteTokenable>>(orgInviteTokenDataProtectorFactory =>
        {
            orgInviteTokenDataProtectorFactory.TryUnprotect(Arg.Is(orgInviteToken), out Arg.Any<OrgUserInviteTokenable>())
                .Returns(callInfo =>
                {
                    callInfo[1] = orgInviteTokenable;
                    return true;
                });
        });

        localFactory.SubstituteService<IOrganizationUserRepository>(orgUserRepository =>
        {
            orgUserRepository.GetByIdAsync(orgUserId)
                .Returns(orgUser);
        });

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            OrgInviteToken = orgInviteToken,
            OrganizationUserId = orgUserId,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, postRegisterFinishHttpContext.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users
            .SingleAsync(u => u.Email == email);

        Assert.NotNull(user);

        // Assert user properties match the request model
        Assert.Equal(email, user.Email);
        Assert.NotEqual(masterPasswordHash, user.MasterPassword);  // We execute server side hashing
        Assert.NotNull(user.MasterPassword);
        Assert.Equal(masterPasswordHint, user.MasterPasswordHint);
        Assert.Equal(userSymmetricKey, user.Key);
        Assert.Equal(userAsymmetricKeys.EncryptedPrivateKey, user.PrivateKey);
        Assert.Equal(userAsymmetricKeys.PublicKey, user.PublicKey);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        Assert.Equal(KdfConstants.PBKDF2_ITERATIONS.Default, user.KdfIterations);
        Assert.Equal(kdfMemory, user.KdfMemory);
        Assert.Equal(kdfParallelism, user.KdfParallelism);
    }


    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithOrgSponsoredFreeFamilyPlanInviteToken_Succeeds(
     [StringLength(1000)] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, string userSymmetricKey,
    KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism, Guid orgSponsorshipId)
    {
        userAsymmetricKeys.AccountKeys = null;

        // Localize factory to just this test.
        var localFactory = new IdentityApplicationFactory();

        // Hardcoded, valid org sponsored free family plan invite token data
        var email = "jsnider+local10000008@bitwarden.com";
        var orgSponsoredFreeFamilyPlanToken = "BWOrganizationSponsorship_CfDJ8HFsgwUNr89EtnCal5H72cx11wdMdD5_FSNMJoXJKp9migo8ZXi2Qx8GOM2b8IccesQEvZxzX_VDvhaaFi1NZc7-5bdadsfaPiwvzy28qwaW5-iF72vncmixArxKt8_FrJCqvn-5Yh45DvUWeOUBl1fPPx6LB4lgf6DcFkFZaHKOxIEywkFWEX9IWsLAfBfhU9K7AYZ02kxLRgXDK_eH3SKY0luoyUbRLBJRq1J9WnAQNcPLx9GOywQDUGRNvQGYmrzpAdq8y3MgUby_XD2NBf4-Vfr_0DIYPlGVJz0Ab1CwKbQ5G9vTXrFbbHQni40GVgohTq6WeVwk-PBMW9kjBw2rHO8QzWUb4whn831y-dEC";

        var orgSponsorship = new OrganizationSponsorship
        {
            Id = orgSponsorshipId,
            PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
            OfferedToEmail = email
        };

        var orgSponsorshipOfferTokenable = new OrganizationSponsorshipOfferTokenable(orgSponsorship) { };

        localFactory.SubstituteService<IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable>>(dataProtectorTokenFactory =>
        {
            dataProtectorTokenFactory.TryUnprotect(Arg.Is(orgSponsoredFreeFamilyPlanToken), out Arg.Any<OrganizationSponsorshipOfferTokenable>())
                .Returns(callInfo =>
                {
                    callInfo[1] = orgSponsorshipOfferTokenable;
                    return true;
                });
        });

        localFactory.SubstituteService<IOrganizationSponsorshipRepository>(organizationSponsorshipRepository =>
        {
            organizationSponsorshipRepository.GetByIdAsync(orgSponsorshipId)
                .Returns(orgSponsorship);
        });

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            OrgSponsoredFreeFamilyPlanToken = orgSponsoredFreeFamilyPlanToken,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, postRegisterFinishHttpContext.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users
            .SingleAsync(u => u.Email == email);

        Assert.NotNull(user);

        // Assert user properties match the request model
        Assert.Equal(email, user.Email);
        Assert.NotEqual(masterPasswordHash, user.MasterPassword);  // We execute server side hashing
        Assert.NotNull(user.MasterPassword);
        Assert.Equal(masterPasswordHint, user.MasterPasswordHint);
        Assert.Equal(userSymmetricKey, user.Key);
        Assert.Equal(userAsymmetricKeys.EncryptedPrivateKey, user.PrivateKey);
        Assert.Equal(userAsymmetricKeys.PublicKey, user.PublicKey);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        Assert.Equal(KdfConstants.PBKDF2_ITERATIONS.Default, user.KdfIterations);
        Assert.Equal(kdfMemory, user.KdfMemory);
        Assert.Equal(kdfParallelism, user.KdfParallelism);
    }

    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithAcceptEmergencyAccessInviteToken_Succeeds(
     [StringLength(1000)] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, string userSymmetricKey,
    KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism, EmergencyAccess emergencyAccess)
    {
        userAsymmetricKeys.AccountKeys = null;

        // Localize factory to just this test.
        var localFactory = new IdentityApplicationFactory();

        // Hardcoded, valid data
        var email = "jsnider+local79813655659549@bitwarden.com";
        var acceptEmergencyAccessInviteToken = "CfDJ8HFsgwUNr89EtnCal5H72cwjvdjWmBp3J0ry7KoG6zDFub-EeoA3cfLBXONq7thKq7QTBh6KJ--jU0Det7t3P9EXqxmEacxIlgFlBgtywIUho9N8nVQeNcltkQO9g0vj_ASshnn6fWK3zpqS6Z8JueVZ2TMtdks5uc7DjZurWFLX27Dpii-UusFD78Z5tCY-D79bkjHy43g1ULk2F2ZtwiJvp3C9QvXW1-12IEsyHHSxU-9RELe-_joo2iDIR-cvMmEfbEXK7uvuzNT2V0r22jalaAKFvd84Gza9Q0YSFn8z_nAJxVqEXsAVKdG8SRN5Wa3K2mdNoBMt20RrzNuuJhe6vzX0yP35HtC4e1YXXzWB";
        var acceptEmergencyAccessId = new Guid("8bc5e574-cef6-4ee7-b9ed-b1e90158c016");

        emergencyAccess.Id = acceptEmergencyAccessId;
        emergencyAccess.Email = email;

        var emergencyAccessInviteTokenable = new EmergencyAccessInviteTokenable(emergencyAccess, 10) { };

        localFactory.SubstituteService<IDataProtectorTokenFactory<EmergencyAccessInviteTokenable>>(dataProtectorTokenFactory =>
        {
            dataProtectorTokenFactory.TryUnprotect(Arg.Is(acceptEmergencyAccessInviteToken), out Arg.Any<EmergencyAccessInviteTokenable>())
                .Returns(callInfo =>
                {
                    callInfo[1] = emergencyAccessInviteTokenable;
                    return true;
                });
        });


        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            AcceptEmergencyAccessInviteToken = acceptEmergencyAccessInviteToken,
            AcceptEmergencyAccessId = acceptEmergencyAccessId,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, postRegisterFinishHttpContext.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users
            .SingleAsync(u => u.Email == email);

        Assert.NotNull(user);

        // Assert user properties match the request model
        Assert.Equal(email, user.Email);
        Assert.NotEqual(masterPasswordHash, user.MasterPassword);  // We execute server side hashing
        Assert.NotNull(user.MasterPassword);
        Assert.Equal(masterPasswordHint, user.MasterPasswordHint);
        Assert.Equal(userSymmetricKey, user.Key);
        Assert.Equal(userAsymmetricKeys.EncryptedPrivateKey, user.PrivateKey);
        Assert.Equal(userAsymmetricKeys.PublicKey, user.PublicKey);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        Assert.Equal(KdfConstants.PBKDF2_ITERATIONS.Default, user.KdfIterations);
        Assert.Equal(kdfMemory, user.KdfMemory);
        Assert.Equal(kdfParallelism, user.KdfParallelism);
    }

    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithProviderInviteToken_Succeeds(
     [StringLength(1000)] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, string userSymmetricKey,
    KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism)
    {
        userAsymmetricKeys.AccountKeys = null;

        // Localize factory to just this test.
        var localFactory = new IdentityApplicationFactory();

        // Hardcoded, valid data
        var email = "jsnider+local253@bitwarden.com";
        var providerUserId = new Guid("c6fdba35-2e52-43b4-8fb7-b211011d154a");
        var nowMillis = CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow);
        var decryptedProviderInviteToken = $"ProviderUserInvite {providerUserId} {email} {nowMillis}";
        // var providerInviteToken = await GetValidProviderInviteToken(localFactory, email, providerUserId);

        // Get the byte array of the plaintext
        var decryptedProviderInviteTokenByteArray = Encoding.UTF8.GetBytes(decryptedProviderInviteToken);

        // Base64 encode the byte array (this is passed to protector.protect(bytes))
        var base64EncodedProviderInvToken = WebEncoders.Base64UrlEncode(decryptedProviderInviteTokenByteArray);

        var mockDataProtector = Substitute.For<IDataProtector>();
        mockDataProtector.Unprotect(Arg.Any<byte[]>()).Returns(decryptedProviderInviteTokenByteArray);

        localFactory.SubstituteService<IDataProtectionProvider>(dataProtectionProvider =>
        {
            dataProtectionProvider.CreateProtector(Arg.Any<string>())
                .Returns(mockDataProtector);
        });

        // As token contains now milliseconds for when it was created, create 1k year timespan for expiration
        // to ensure token is valid for a good long while.
        localFactory.UpdateConfiguration("globalSettings:OrganizationInviteExpirationHours", "8760000");

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            ProviderInviteToken = base64EncodedProviderInvToken,
            ProviderUserId = providerUserId,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, postRegisterFinishHttpContext.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users
            .SingleAsync(u => u.Email == email);

        Assert.NotNull(user);

        // Assert user properties match the request model
        Assert.Equal(email, user.Email);
        Assert.NotEqual(masterPasswordHash, user.MasterPassword);  // We execute server side hashing
        Assert.NotNull(user.MasterPassword);
        Assert.Equal(masterPasswordHint, user.MasterPasswordHint);
        Assert.Equal(userSymmetricKey, user.Key);
        Assert.Equal(userAsymmetricKeys.EncryptedPrivateKey, user.PrivateKey);
        Assert.Equal(userAsymmetricKeys.PublicKey, user.PublicKey);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        Assert.Equal(KdfConstants.PBKDF2_ITERATIONS.Default, user.KdfIterations);
        Assert.Equal(kdfMemory, user.KdfMemory);
        Assert.Equal(kdfParallelism, user.KdfParallelism);
    }


    [Theory, BitAutoData]
    public async Task RegisterViaSalesAssistedToken_WithDisabledRegistration_Succeeds(
        string name,
        [StringLength(1000)] string masterPasswordHash,
        [StringLength(50)] string masterPasswordHint,
        string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys,
        int kdfMemory,
        int kdfParallelism)
    {
        userAsymmetricKeys.AccountKeys = null;

        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:disableUserRegistration", "true");

        var email = $"test+sales+{name}@email.com";
        const string salesAssistedToken = "fake-sales-assisted-token";

        var tokenable = new SalesAssistedRegistrationTokenable
        {
            Email = email,
            Name = name,
            ExpirationDate = DateTime.UtcNow.AddDays(5)
        };

        localFactory.SubstituteService<IDataProtectorTokenFactory<SalesAssistedRegistrationTokenable>>(factory =>
        {
            factory.TryUnprotect(Arg.Is(salesAssistedToken), out Arg.Any<SalesAssistedRegistrationTokenable>())
                .Returns(callInfo =>
                {
                    callInfo[1] = tokenable;
                    return true;
                });
        });

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            SalesAssistedToken = salesAssistedToken,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, postRegisterFinishHttpContext.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users.SingleAsync(u => u.Email == email);

        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.Equal(name, user.Name);
        Assert.NotEqual(masterPasswordHash, user.MasterPassword);
        Assert.NotNull(user.MasterPassword);
    }

    [Theory, BitAutoData]
    public async Task RegisterViaSalesAssistedToken_WithInvalidToken_Returns400(
        string name,
        [StringLength(1000)] string masterPasswordHash,
        string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys)
    {
        userAsymmetricKeys.AccountKeys = null;

        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:disableUserRegistration", "true");

        var email = $"test+sales+{name}@email.com";
        const string salesAssistedToken = "invalid-token";

        localFactory.SubstituteService<IDataProtectorTokenFactory<SalesAssistedRegistrationTokenable>>(factory =>
        {
            factory.TryUnprotect(Arg.Any<string>(), out Arg.Any<SalesAssistedRegistrationTokenable>())
                .Returns(callInfo =>
                {
                    callInfo[1] = null;
                    return false;
                });
        });

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            SalesAssistedToken = salesAssistedToken,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status400BadRequest, postRegisterFinishHttpContext.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task RegisterViaSalesAssistedToken_WithEmailMismatch_Returns400(
        string name,
        [StringLength(1000)] string masterPasswordHash,
        string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys)
    {
        userAsymmetricKeys.AccountKeys = null;

        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:disableUserRegistration", "true");

        var email = $"test+sales+{name}@email.com";
        var tokenEmail = $"other+{name}@email.com";
        const string salesAssistedToken = "fake-sales-assisted-token";

        // Token is valid but bound to a different email address — TokenIsValid(email) must reject this.
        var tokenable = new SalesAssistedRegistrationTokenable
        {
            Email = tokenEmail,
            ExpirationDate = DateTime.UtcNow.AddDays(5)
        };

        localFactory.SubstituteService<IDataProtectorTokenFactory<SalesAssistedRegistrationTokenable>>(factory =>
        {
            factory.TryUnprotect(Arg.Is(salesAssistedToken), out Arg.Any<SalesAssistedRegistrationTokenable>())
                .Returns(callInfo =>
                {
                    callInfo[1] = tokenable;
                    return true;
                });
        });

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,  // does not match tokenEmail
            MasterPasswordHash = masterPasswordHash,
            SalesAssistedToken = salesAssistedToken,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status400BadRequest, postRegisterFinishHttpContext.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task PostRegisterVerificationEmailClicked_Success(
        [Required, StringLength(20)] string name,
        string emailVerificationToken)
    {
        // Arrange
        // Localize substitutions to this test.
        var localFactory = new IdentityApplicationFactory();

        var email = $"test+register+{name}@email.com";
        var registrationEmailVerificationTokenable = new RegistrationEmailVerificationTokenable(email);

        localFactory.SubstituteService<IDataProtectorTokenFactory<RegistrationEmailVerificationTokenable>>(emailVerificationTokenDataProtectorFactory =>
        {
            emailVerificationTokenDataProtectorFactory.TryUnprotect(Arg.Is(emailVerificationToken), out Arg.Any<RegistrationEmailVerificationTokenable>())
                .Returns(callInfo =>
                {
                    callInfo[1] = registrationEmailVerificationTokenable;
                    return true;
                });
        });

        var requestModel = new RegisterVerificationEmailClickedRequestModel
        {
            Email = email,
            EmailVerificationToken = emailVerificationToken
        };

        // Act
        var httpContext = await localFactory.PostRegisterVerificationEmailClicked(requestModel);

        var body = await httpContext.ReadBodyAsStringAsync();

        // Assert
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    private async Task<User> CreateUserAsync(string email, string name, IdentityApplicationFactory factory = null, string masterPasswordSalt = null)
    {
        var factoryToUse = factory ?? _factory;

        var userRepository = factoryToUse.Services.GetRequiredService<IUserRepository>();

        var user = new User
        {
            Email = email,
            Id = Guid.NewGuid(),
            Name = name,
            SecurityStamp = Guid.NewGuid().ToString(),
            ApiKey = "test_api_key",
            MasterPasswordSalt = masterPasswordSalt,
        };

        await userRepository.CreateAsync(user);

        return user;
    }

    [Theory, BitAutoData]
    public async Task PostPrelogin_WhenUserExistsWithSalt_ReturnsStoredSalt([Required] string name)
    {
        var localFactory = new IdentityApplicationFactory();
        var email = $"test+prelogin+{name}@email.com";
        await CreateUserAsync(email, name, localFactory, masterPasswordSalt: email);

        var context = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = email });

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        Assert.Equal(email, body.RootElement.GetProperty("salt").GetString());
    }

    [Theory, BitAutoData]
    public async Task PostPrelogin_WhenUserExistsWithNullSalt_ReturnsNullSalt([Required] string name)
    {
        var localFactory = new IdentityApplicationFactory();
        var email = $"test+prelogin+{name}@email.com";
        await CreateUserAsync(email, name, localFactory, masterPasswordSalt: null);

        var context = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = email });

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("salt").ValueKind);
    }

    [Theory, BitAutoData]
    public async Task PostPrelogin_WhenUserDoesNotExistAndDefaultHashKeyConfigured_ReturnsDeterministicResult([Required] string name)
    {
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:kdfDefaultHashKey", "test-default-hash-key");
        var email = $"nonexistent+prelogin+{name}@email.com";

        var first = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = email });
        var second = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = email });

        Assert.Equal(StatusCodes.Status200OK, first.Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, second.Response.StatusCode);
        using var firstBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(first);
        using var secondBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(second);
        Assert.Equal(firstBody.RootElement.GetProperty("salt").GetRawText(), secondBody.RootElement.GetProperty("salt").GetRawText());
        Assert.Equal(firstBody.RootElement.GetProperty("kdf").GetRawText(), secondBody.RootElement.GetProperty("kdf").GetRawText());
        Assert.Equal(firstBody.RootElement.GetProperty("kdfIterations").GetRawText(), secondBody.RootElement.GetProperty("kdfIterations").GetRawText());
    }

    [Theory, BitAutoData]
    public async Task PostPrelogin_WhenUserDoesNotExistAndNoDefaultHashKey_ReturnsEmailAsSalt([Required] string name)
    {
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:kdfDefaultHashKey", null);
        var email = $"nonexistent+prelogin+{name}@email.com";

        var context = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = email });

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        Assert.Equal(email, body.RootElement.GetProperty("salt").GetString());
    }

    [Theory, BitAutoData]
    public async Task PostPrelogin_WhenUserDoesNotExist_ReturnsSaltIndependentOfInputCasing([Required] string name)
    {
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:kdfDefaultHashKey", "test-default-hash-key");
        var lowercaseEmail = $"nonexistent+prelogin+{name}@email.com";
        var mixedCaseEmail = lowercaseEmail.ToUpperInvariant();

        var lowercase = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = lowercaseEmail });
        var mixedCase = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = mixedCaseEmail });

        Assert.Equal(StatusCodes.Status200OK, lowercase.Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, mixedCase.Response.StatusCode);
        using var lowercaseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(lowercase);
        using var mixedCaseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(mixedCase);
        Assert.Equal(lowercaseBody.RootElement.GetProperty("salt").GetRawText(), mixedCaseBody.RootElement.GetProperty("salt").GetRawText());
    }

}
