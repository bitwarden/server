using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.AccountsTestHelpers;

namespace Bit.Identity.IntegrationTest.Controllers.Accounts;

public class RegisterFinishOpenOrgInviteTests
{
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
    public async Task RegistrationWithEmailVerification_WithOpenOrgInviteAndEmailedToken_MarksEmailVerified(
        [Required] string name, bool receiveMarketingEmails,
        [StringLength(1000), Required] string masterPasswordHash, [StringLength(50)] string masterPasswordHint,
        [Required] string userSymmetricKey, [Required] KeysRequestModel userAsymmetricKeys,
        int kdfMemory, int kdfParallelism)
    {
        userAsymmetricKeys.AccountKeys = null;
        var localFactory = new IdentityApplicationFactory();

        var email = $"test+openinvite+emailed+{name}@email.com";
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
        Assert.True(user.EmailVerified);
    }

    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithOpenOrgInviteAndEnableEmailVerificationFalse_DoesNotMarkEmailVerified(
        [Required] string name, bool receiveMarketingEmails,
        [StringLength(1000), Required] string masterPasswordHash, [StringLength(50)] string masterPasswordHint,
        [Required] string userSymmetricKey, [Required] KeysRequestModel userAsymmetricKeys,
        int kdfMemory, int kdfParallelism)
    {
        // With email verification disabled, the token comes back in the response body and no email
        // is sent, so registering with it proves nothing about mailbox ownership.
        userAsymmetricKeys.AccountKeys = null;
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:enableEmailVerification", "false");

        var email = $"test+openinvite+noemail+{name}@email.com";
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
        Assert.Equal(StatusCodes.Status200OK, sendCtx.Response.StatusCode);
        var emailVerificationToken = await sendCtx.ReadBodyAsStringAsync();
        Assert.False(localFactory.RegistrationTokens.ContainsKey(email));

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
        Assert.Equal(name, user.Name);
        Assert.False(user.EmailVerified);
    }

    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithOpenOrgInviteAndTokenIssuedWithEnableEmailVerificationFalse_FinishedAfterSettingEnabled_DoesNotMarkEmailVerified(
        [Required] string name, bool receiveMarketingEmails,
        [StringLength(1000), Required] string masterPasswordHash, [StringLength(50)] string masterPasswordHint,
        [Required] string userSymmetricKey, [Required] KeysRequestModel userAsymmetricKeys,
        int kdfMemory, int kdfParallelism)
    {
        // Whether the email is verified depends on how the token was delivered, not on the
        // setting's value when registration finishes.
        userAsymmetricKeys.AccountKeys = null;
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:enableEmailVerification", "false");

        var email = $"test+openinvite+settingflip+{name}@email.com";
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
        Assert.Equal(StatusCodes.Status200OK, sendCtx.Response.StatusCode);
        var emailVerificationToken = await sendCtx.ReadBodyAsStringAsync();

        // GlobalSettings is a singleton, so this changes the value every command reads from here on.
        localFactory.GetService<GlobalSettings>().EnableEmailVerification = true;

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
        Assert.False(user.EmailVerified);
    }

    // TODO: PM-45479 - Delete this test.
    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task RegistrationWithEmailVerification_WithOpenOrgInviteAndTokenWithoutEmailSent_MarksEmailVerifiedPerEnableEmailVerification(
        bool enableEmailVerification,
        [Required] string name,
        [StringLength(1000), Required] string masterPasswordHash, [StringLength(50)] string masterPasswordHint,
        [Required] string userSymmetricKey, [Required] KeysRequestModel userAsymmetricKeys,
        int kdfMemory, int kdfParallelism)
    {
        // A token issued before EmailSent existed was emailed only when email verification was enabled.
        userAsymmetricKeys.AccountKeys = null;
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:enableEmailVerification", enableEmailVerification.ToString());

        var email = $"test+openinvite+noemailsent+{name}@email.com";
        var (_, inviteLink) = await SeedOrgWithInviteLinkAsync(localFactory, allowedDomains: new[] { "email.com" });
        var emailVerificationToken = ProtectRegistrationTokenWithoutEmailSent(localFactory, email, name);

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
        Assert.Equal(name, user.Name);
        Assert.Equal(enableEmailVerification, user.EmailVerified);
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
}
