using System.Net;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;
using static Bit.Api.IntegrationTest.Auth.Helpers.EmailChangeTestHelpers;
using static Bit.Api.IntegrationTest.Auth.Helpers.MasterPasswordPayloads;

namespace Bit.Api.IntegrationTest.Auth.Controllers.Accounts;

public class AccountsControllerEmailChangeTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IMailService _mailService;
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService;
    private readonly IOrganizationRepository _organizationRepository;

    private string _ownerEmail = null!;

    public AccountsControllerEmailChangeTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<Bitwarden.Server.Sdk.Features.IFeatureService>(_ => { });
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _factory.SubstituteService<IMailService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _pushNotificationService = _factory.GetService<IPushNotificationService>();
        _mailService = _factory.GetService<IMailService>();
        _featureService = _factory.GetService<Bitwarden.Server.Sdk.Features.IFeatureService>();
        _organizationRepository = _factory.GetService<IOrganizationRepository>();
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // ===== Email change: self-service path (PM30806_SelfServiceChangeEmailCommand flag ON) =====
    // These tests pin the flag ON to exercise SelfServiceChangeEmailCommand, which changes the
    // email only — the master password, derived security stamp, and wrapped user key are left
    // untouched. They cover both POST /accounts/email and POST /accounts/email-token and share
    // the PostEmailSelfServiceAsync / PostEmailTokenAsync helpers.
    //
    // TODO: PM-39120 - On flag cleanup, these become the canonical email-change tests: drop the
    // "_SelfServiceFlagOn_" qualifier from each name and remove the per-test
    // _featureService.IsEnabled(...).Returns(true) setup. Delete the _SelfServiceFlagOff_ (legacy)
    // tests wholesale.
    [Fact]
    public async Task PostEmail_SelfServiceFlagOn_Success_UpdatesEmailWithoutTouchingPasswordOrKey()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM30806_SelfServiceChangeEmailCommand).Returns(true);
        var newEmail = $"new-email-{Guid.NewGuid()}@bitwarden.com";
        await _loginHelper.LoginAsync(_ownerEmail);

        var userBefore = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(userBefore);
        var originalKey = userBefore.Key;
        var originalMasterPassword = userBefore.MasterPassword;
        var originalSecurityStamp = userBefore.SecurityStamp;

        var userManager = _factory.GetService<UserManager<User>>();
        var token = await userManager.GenerateChangeEmailTokenAsync(userBefore, newEmail);

        var response = await PostEmailSelfServiceAsync(_client, newEmail, token, MasterPasswordHash);

        response.EnsureSuccessStatusCode();

        var updatedUser = await _userRepository.GetByEmailAsync(newEmail);
        Assert.NotNull(updatedUser);
        Assert.Equal(newEmail, updatedUser.Email);
        Assert.True(updatedUser.EmailVerified);
        // Post-decoupling self-service flow: the master password, derived security stamp, and
        // wrapped user key MUST NOT rotate just because the email changed.
        Assert.Equal(originalKey, updatedUser.Key);
        Assert.Equal(originalMasterPassword, updatedUser.MasterPassword);
        Assert.Equal(originalSecurityStamp, updatedUser.SecurityStamp);

        await _pushNotificationService.Received(1)
            .PushAsync(Arg.Is<PushNotification<UserPushNotification>>(n => n.Type == PushType.SyncSettings && n.TargetId == updatedUser.Id));
        await _pushNotificationService.DidNotReceive()
            .PushAsync(Arg.Is<PushNotification<LogOutPushNotification>>(n => n.TargetId == updatedUser.Id));
    }

    [Fact]
    public async Task PostEmail_SelfServiceFlagOn_InvalidMasterPassword_BadRequest_AndDoesNotChangeEmail()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM30806_SelfServiceChangeEmailCommand).Returns(true);
        var newEmail = $"new-email-{Guid.NewGuid()}@bitwarden.com";
        await _loginHelper.LoginAsync(_ownerEmail);

        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);

        var userManager = _factory.GetService<UserManager<User>>();
        var token = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);

        var response = await PostEmailSelfServiceAsync(_client, newEmail, token, "wrong-master-password-hash");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var unchangedUser = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(unchangedUser);
        Assert.Equal(_ownerEmail, unchangedUser.Email);
    }

    [Fact]
    public async Task PostEmail_SelfServiceFlagOn_InvalidToken_BadRequest_AndDoesNotChangeEmail()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM30806_SelfServiceChangeEmailCommand).Returns(true);
        var newEmail = $"new-email-{Guid.NewGuid()}@bitwarden.com";
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await PostEmailSelfServiceAsync(_client, newEmail, "not-a-valid-token", MasterPasswordHash);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var unchangedUser = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(unchangedUser);
        Assert.Equal(_ownerEmail, unchangedUser.Email);
    }

    [Fact]
    public async Task PostEmail_SelfServiceFlagOn_KeyConnectorUser_BadRequest_AndDoesNotChangeEmail()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM30806_SelfServiceChangeEmailCommand).Returns(true);
        var newEmail = $"new-email-{Guid.NewGuid()}@bitwarden.com";
        await _loginHelper.LoginAsync(_ownerEmail);

        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);
        user.UsesKeyConnector = true;
        await _userRepository.ReplaceAsync(user);

        var userManager = _factory.GetService<UserManager<User>>();
        var token = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);

        var response = await PostEmailSelfServiceAsync(_client, newEmail, token, MasterPasswordHash);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var unchangedUser = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(unchangedUser);
        Assert.Equal(_ownerEmail, unchangedUser.Email);
    }

    [Theory]
    [InlineData(OrganizationUserType.Owner)]
    [InlineData(OrganizationUserType.Admin)]
    public async Task PostEmail_SelfServiceFlagOn_KeyConnectorOrgAdminOrOwner_NotUsingKeyConnector_CanChangeEmail(
        OrganizationUserType userType)
    {
        // An owner/admin of a Key Connector organization who doesn't personally use Key Connector must be
        // able to change their email.
        _featureService.IsEnabled(FeatureFlagKeys.PM30806_SelfServiceChangeEmailCommand).Returns(true);

        // Make the seeded account the owner of a Key Connector organization, then re-login so the
        // access token carries the owner claim that ICurrentContext.Organizations is built from.
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            PlanType.EnterpriseAnnually, _ownerEmail, passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);
        organization.UseKeyConnector = true;
        organization.UseSso = true;
        organization.Identifier = $"kc-org-{Guid.NewGuid()}";
        await _organizationRepository.ReplaceAsync(organization);

        (var currentUserEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory,
            organization.Id,
            userType);

        await _loginHelper.LoginAsync(currentUserEmail);

        var newEmail = $"new-email-{Guid.NewGuid()}@bitwarden.com";
        var userBefore = await _userRepository.GetByEmailAsync(currentUserEmail);
        Assert.NotNull(userBefore);
        // The org uses Key Connector, but the acting member themselves does not.
        Assert.False(userBefore.UsesKeyConnector);

        var userManager = _factory.GetService<UserManager<User>>();
        var token = await userManager.GenerateChangeEmailTokenAsync(userBefore, newEmail);

        var response = await PostEmailSelfServiceAsync(_client, newEmail, token, MasterPasswordHash);

        response.EnsureSuccessStatusCode();

        var updatedUser = await _userRepository.GetByEmailAsync(newEmail);
        Assert.NotNull(updatedUser);
        Assert.Equal(newEmail, updatedUser.Email);
        Assert.True(updatedUser.EmailVerified);
    }

    [Fact]
    public async Task PostEmail_SelfServiceFlagOn_UnclaimedUserAtBlockedDomain_ChangingWithinSameDomain_IsBlocked()
    {
        // PM-30806 item 2 — regression guard.
        //
        // A user registers at a domain while it is still unclaimed and never
        // joins the organization. An organization later VERIFIES that domain and enables the
        // BlockClaimedDomainAccountCreation policy. The user then changes to a different local-part at the
        // SAME (now-blocked) domain.
        //
        // The change must be DENIED: a same-domain change no longer bypasses the block-policy gate.

        _featureService.IsEnabled(FeatureFlagKeys.PM30806_SelfServiceChangeEmailCommand).Returns(true);

        var blockedDomain = OrganizationTestHelpers.GenerateRandomDomain();

        // Grandfathered, non-member user whose current email lives at the (soon-to-be) blocked domain.
        var grandfatheredEmail = $"grandfathered-{Guid.NewGuid()}@{blockedDomain}";
        await _factory.LoginWithNewAccount(grandfatheredEmail);

        // A SEPARATE organization claims the domain: verify it and enable the block policy.
        // The grandfathered user is intentionally NOT a member of this org.
        var orgOwnerEmail = $"org-owner-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(orgOwnerEmail);
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            PlanType.EnterpriseAnnually, orgOwnerEmail, passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);
        organization.UsePolicies = true;
        organization.UseOrganizationDomains = true;
        await _organizationRepository.ReplaceAsync(organization);

        await OrganizationTestHelpers.CreateVerifiedDomainAsync(_factory, organization.Id, blockedDomain);

        var policyRepository = _factory.GetService<IPolicyRepository>();
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = organization.Id,
            Type = PolicyType.BlockClaimedDomainAccountCreation,
            Enabled = true
        });

        // Act on the inherited domain user, changing to a new local-part at the SAME blocked domain.
        await _loginHelper.LoginAsync(grandfatheredEmail);
        var newEmail = $"changed-{Guid.NewGuid()}@{blockedDomain}";

        var userBefore = await _userRepository.GetByEmailAsync(grandfatheredEmail);
        Assert.NotNull(userBefore);

        var userManager = _factory.GetService<UserManager<User>>();
        var token = await userManager.GenerateChangeEmailTokenAsync(userBefore, newEmail);

        var response = await PostEmailSelfServiceAsync(_client, newEmail, token, MasterPasswordHash);

        // The block-claimed-domain policy denies the change, and the email is left unchanged.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var unchangedUser = await _userRepository.GetByEmailAsync(grandfatheredEmail);
        Assert.NotNull(unchangedUser);
        Assert.Equal(grandfatheredEmail, unchangedUser.Email);
    }

    [Fact]
    public async Task PostEmailToken_SelfServiceFlagOn_NewEmailAvailable_SendsChangeEmailWithToken()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM30806_SelfServiceChangeEmailCommand).Returns(true);
        var newEmail = $"new-email-{Guid.NewGuid()}@bitwarden.com";
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await PostEmailTokenAsync(newEmail, MasterPasswordHash);

        response.EnsureSuccessStatusCode();

        await _mailService.Received(1)
            .SendChangeEmailEmailAsync(newEmail, Arg.Is<string>(t => !string.IsNullOrEmpty(t)));
        await _mailService.DidNotReceive()
            .SendChangeEmailAlreadyExistsEmailAsync(Arg.Any<string>(), newEmail);

        var unchangedUser = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(unchangedUser);
        Assert.Equal(_ownerEmail, unchangedUser.Email);
    }

    [Fact]
    public async Task PostEmailToken_SelfServiceFlagOn_NewEmailInUse_NotifiesCurrentEmailAndDoesNotIssueToken()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM30806_SelfServiceChangeEmailCommand).Returns(true);

        // Register a second account to take the target email, then log back in as the owner.
        var existingEmail = $"existing-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(existingEmail);
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await PostEmailTokenAsync(existingEmail, MasterPasswordHash);

        // Success is returned to the caller so the API surface does not leak whether the new
        // email is already registered; the existing account is notified out-of-band instead.
        response.EnsureSuccessStatusCode();

        await _mailService.Received(1)
            .SendChangeEmailAlreadyExistsEmailAsync(_ownerEmail, existingEmail);
        await _mailService.DidNotReceive()
            .SendChangeEmailEmailAsync(existingEmail, Arg.Any<string>());

        var unchangedUser = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(unchangedUser);
        Assert.Equal(_ownerEmail, unchangedUser.Email);
    }

    [Fact]
    public async Task PostEmailToken_SelfServiceFlagOn_InvalidMasterPassword_BadRequest_AndDoesNotSendMail()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM30806_SelfServiceChangeEmailCommand).Returns(true);
        var newEmail = $"new-email-{Guid.NewGuid()}@bitwarden.com";
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await PostEmailTokenAsync(newEmail, "wrong-master-password-hash");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await _mailService.DidNotReceive()
            .SendChangeEmailEmailAsync(newEmail, Arg.Any<string>());
        await _mailService.DidNotReceive()
            .SendChangeEmailAlreadyExistsEmailAsync(Arg.Any<string>(), newEmail);
    }

    private async Task<HttpResponseMessage> PostEmailTokenAsync(string newEmail, string masterPasswordHash)
    {
        var requestModel = new EmailTokenRequestModel
        {
            MasterPasswordHash = masterPasswordHash,
            NewEmail = newEmail
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/email-token");
        message.Content = JsonContent.Create(requestModel);

        return await _client.SendAsync(message);
    }
}
