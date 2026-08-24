using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.IntegrationTest.Auth.Helpers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerAcceptInviteLinkTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private const string _invite = "opaque-invite-blob";

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public OrganizationUsersControllerAcceptInviteLinkTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService
                .IsEnabled(FeatureFlagKeys.GenerateInviteLink)
                .Returns(true);
        });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"integration-test{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(
            _factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        _organization.UseInviteLinks = true;
        await organizationRepository.ReplaceAsync(_organization);

        await _loginHelper.LoginAsync(_ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AcceptInviteLink_WithValidRequest_ReturnsOk()
    {
        var createRequest = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["example.com"],
            Invite = _invite,
            SupportsConfirmation = false,
        };
        var createResponse = await _client.PostAsJsonAsync(
            $"/organizations/{_organization.Id}/invite-link", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(created);

        var joinerEmail = $"integration-test{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(joinerEmail);
        var joinerClient = _factory.CreateClient();
        var joinerLoginHelper = new LoginHelper(_factory, joinerClient);
        await joinerLoginHelper.LoginAsync(joinerEmail);

        var acceptRequest = new AcceptOrganizationInviteLinkRequestModel { OrganizationId = created.OrganizationId, Code = created.Code };
        var response = await joinerClient.PostAsJsonAsync(
            "/organizations/users/invite-link/accept", acceptRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var userRepository = _factory.GetService<IUserRepository>();
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var joiner = await userRepository.GetByEmailAsync(joinerEmail);
        Assert.NotNull(joiner);

        var organizationUser = await organizationUserRepository.GetByOrganizationAsync(_organization.Id, joiner.Id);
        Assert.NotNull(organizationUser);
        Assert.Equal(OrganizationUserStatusType.Accepted, organizationUser.Status);
    }

    [Fact]
    public async Task AcceptInviteLink_WithAccountRecoveryAutoEnrollEnabled_EnrollsUserInAccountRecovery()
    {
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        _organization.UseResetPassword = true;
        _organization.UsePolicies = true;
        await organizationRepository.ReplaceAsync(_organization);

        var policyRepository = _factory.GetService<IPolicyRepository>();
        var resetPasswordPolicy = new Policy
        {
            OrganizationId = _organization.Id,
            Type = PolicyType.ResetPassword,
            Enabled = true,
        };
        resetPasswordPolicy.SetDataModel(new ResetPasswordDataModel { AutoEnrollEnabled = true });
        await policyRepository.CreateAsync(resetPasswordPolicy);

        var createRequest = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["example.com"],
            Invite = _invite,
            SupportsConfirmation = false,
        };
        var createResponse = await _client.PostAsJsonAsync(
            $"/organizations/{_organization.Id}/invite-link", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(created);

        var joinerEmail = $"integration-test{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(joinerEmail);
        var joinerClient = _factory.CreateClient();
        var joinerLoginHelper = new LoginHelper(_factory, joinerClient);
        await joinerLoginHelper.LoginAsync(joinerEmail);

        const string resetPasswordKey = "2.reset-password-key";
        var acceptRequest = new AcceptOrganizationInviteLinkRequestModel
        {
            OrganizationId = created.OrganizationId,
            Code = created.Code,
            ResetPasswordKey = resetPasswordKey,
        };
        var response = await joinerClient.PostAsJsonAsync(
            "/organizations/users/invite-link/accept", acceptRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var userRepository = _factory.GetService<IUserRepository>();
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var joiner = await userRepository.GetByEmailAsync(joinerEmail);
        Assert.NotNull(joiner);

        var organizationUser = await organizationUserRepository.GetByOrganizationAsync(_organization.Id, joiner.Id);
        Assert.NotNull(organizationUser);
        Assert.Equal(OrganizationUserStatusType.Accepted, organizationUser.Status);
        Assert.Equal(resetPasswordKey, organizationUser.ResetPasswordKey);
    }

    // A user without two-step login must not be able to accept an invite link into an organization that
    // enforces the Require Two-Factor Authentication policy, mirroring the normal invite accept flow.
    [Fact]
    public async Task AcceptInviteLink_WhenOrganizationRequiresTwoFactor_AndJoinerHasNoTwoFactor_IsRejected()
    {
        await EnablePolicyAsync(PolicyType.TwoFactorAuthentication);

        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();

        var response = await joinerClient.PostAsJsonAsync(
            "/organizations/users/invite-link/accept",
            new AcceptOrganizationInviteLinkRequestModel { OrganizationId = created.OrganizationId, Code = created.Code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNotMemberAsync(joinerEmail);
    }

    // The Single Organization policy of the target organization must block a user who already belongs to
    // another organization from accepting its invite link.
    [Fact]
    public async Task AcceptInviteLink_WhenTargetOrganizationEnforcesSingleOrg_AndJoinerBelongsToAnotherOrg_IsRejected()
    {
        await EnablePolicyAsync(PolicyType.SingleOrg);

        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();
        await AddJoinerToAnotherOrganizationAsync(joinerEmail);

        var response = await joinerClient.PostAsJsonAsync(
            "/organizations/users/invite-link/accept",
            new AcceptOrganizationInviteLinkRequestModel { OrganizationId = created.OrganizationId, Code = created.Code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNotMemberAsync(joinerEmail);
    }

    // The Automatic User Confirmation policy of the target organization must block a user who already belongs
    // to another organization from accepting its invite link.
    [Fact]
    public async Task AcceptInviteLink_WhenTargetOrganizationEnforcesAutoConfirm_AndJoinerBelongsToAnotherOrg_IsRejected()
    {
        await EnablePolicyAsync(PolicyType.AutomaticUserConfirmation);

        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();
        await AddJoinerToAnotherOrganizationAsync(joinerEmail);

        var response = await joinerClient.PostAsJsonAsync(
            "/organizations/users/invite-link/accept",
            new AcceptOrganizationInviteLinkRequestModel { OrganizationId = created.OrganizationId, Code = created.Code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNotMemberAsync(joinerEmail);
    }

    // A joiner who has two-step login enabled may accept an invite link into an org that requires 2FA.
    [Fact]
    public async Task AcceptInviteLink_WhenOrganizationRequiresTwoFactor_AndJoinerHasTwoFactor_Succeeds()
    {
        await EnablePolicyAsync(PolicyType.TwoFactorAuthentication);

        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();
        await SetJoinerTwoFactorEnabledAsync(joinerEmail);

        var response = await AcceptAsync(joinerClient, created);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertAcceptedMemberAsync(joinerEmail);
    }

    // Cross-org: a joiner who belongs to another org that enforces Single Organization cannot join a new org.
    [Fact]
    public async Task AcceptInviteLink_WhenJoinerBelongsToAnotherOrgWithSingleOrgPolicy_IsRejected()
    {
        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();
        await AddJoinerToAnotherOrganizationAsync(joinerEmail, PolicyType.SingleOrg);

        var response = await AcceptAsync(joinerClient, created);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNotMemberAsync(joinerEmail);
    }

    // A joiner who is not a member of any other org may accept an invite link into a Single-Org org.
    [Fact]
    public async Task AcceptInviteLink_WhenTargetOrganizationEnforcesSingleOrg_AndJoinerHasNoOtherOrg_Succeeds()
    {
        await EnablePolicyAsync(PolicyType.SingleOrg);

        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();

        var response = await AcceptAsync(joinerClient, created);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertAcceptedMemberAsync(joinerEmail);
    }

    // Cross-org: a joiner who belongs to another org that enforces Auto-Confirm cannot join a new org.
    [Fact]
    public async Task AcceptInviteLink_WhenJoinerBelongsToAnotherOrgWithAutoConfirmPolicy_IsRejected()
    {
        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();
        await AddJoinerToAnotherOrganizationAsync(joinerEmail, PolicyType.AutomaticUserConfirmation);

        var response = await AcceptAsync(joinerClient, created);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNotMemberAsync(joinerEmail);
    }

    // A joiner who is not a member of any other org may accept an invite link into an Auto-Confirm org.
    [Fact]
    public async Task AcceptInviteLink_WhenTargetOrganizationEnforcesAutoConfirm_AndJoinerHasNoOtherOrg_Succeeds()
    {
        await EnablePolicyAsync(PolicyType.AutomaticUserConfirmation);

        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();

        var response = await AcceptAsync(joinerClient, created);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertAcceptedMemberAsync(joinerEmail);
    }

    // Existing-invitation path: accepting a pending email invitation via the link links and accepts the user.
    [Fact]
    public async Task AcceptInviteLink_WithExistingInvitation_WithValidRequest_ReturnsOk()
    {
        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();
        await CreatePendingEmailInvitationAsync(joinerEmail);

        var response = await AcceptAsync(joinerClient, created);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var organizationUser = await AssertAcceptedMemberAsync(joinerEmail);
        Assert.Null(organizationUser.Email);
    }

    // Existing-invitation path: 2FA policy is still enforced when accepting a pending email invitation.
    [Fact]
    public async Task AcceptInviteLink_WithExistingInvitation_WhenOrganizationRequiresTwoFactor_AndJoinerHasNoTwoFactor_IsRejected()
    {
        await EnablePolicyAsync(PolicyType.TwoFactorAuthentication);

        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();
        await CreatePendingEmailInvitationAsync(joinerEmail);

        var response = await AcceptAsync(joinerClient, created);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertStillInvitedAsync(joinerEmail);
    }

    // Existing-invitation path: a joiner with 2FA may accept a pending email invitation into a 2FA org.
    [Fact]
    public async Task AcceptInviteLink_WithExistingInvitation_WhenOrganizationRequiresTwoFactor_AndJoinerHasTwoFactor_Succeeds()
    {
        await EnablePolicyAsync(PolicyType.TwoFactorAuthentication);

        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();
        await SetJoinerTwoFactorEnabledAsync(joinerEmail);
        await CreatePendingEmailInvitationAsync(joinerEmail);

        var response = await AcceptAsync(joinerClient, created);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertAcceptedMemberAsync(joinerEmail);
    }

    // Existing-invitation path: Single Org is still enforced when accepting a pending email invitation.
    [Fact]
    public async Task AcceptInviteLink_WithExistingInvitation_WhenTargetOrganizationEnforcesSingleOrg_AndJoinerBelongsToAnotherOrg_IsRejected()
    {
        await EnablePolicyAsync(PolicyType.SingleOrg);

        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();
        await AddJoinerToAnotherOrganizationAsync(joinerEmail);
        await CreatePendingEmailInvitationAsync(joinerEmail);

        var response = await AcceptAsync(joinerClient, created);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertStillInvitedAsync(joinerEmail);
    }

    // Existing-invitation path: Auto-Confirm is still enforced when accepting a pending email invitation.
    [Fact]
    public async Task AcceptInviteLink_WithExistingInvitation_WhenTargetOrganizationEnforcesAutoConfirm_AndJoinerBelongsToAnotherOrg_IsRejected()
    {
        await EnablePolicyAsync(PolicyType.AutomaticUserConfirmation);

        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();
        await AddJoinerToAnotherOrganizationAsync(joinerEmail);
        await CreatePendingEmailInvitationAsync(joinerEmail);

        var response = await AcceptAsync(joinerClient, created);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertStillInvitedAsync(joinerEmail);
    }

    // Existing-invitation path: account-recovery auto-enroll still enrolls the user when accepting a
    // pending email invitation (the auto-enroll check runs for both membership branches).
    [Fact]
    public async Task AcceptInviteLink_WithExistingInvitation_AndAccountRecoveryAutoEnrollEnabled_EnrollsUserInAccountRecovery()
    {
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        _organization.UseResetPassword = true;
        _organization.UsePolicies = true;
        await organizationRepository.ReplaceAsync(_organization);

        var policyRepository = _factory.GetService<IPolicyRepository>();
        var resetPasswordPolicy = new Policy
        {
            OrganizationId = _organization.Id,
            Type = PolicyType.ResetPassword,
            Enabled = true,
        };
        resetPasswordPolicy.SetDataModel(new ResetPasswordDataModel { AutoEnrollEnabled = true });
        await policyRepository.CreateAsync(resetPasswordPolicy);

        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();
        await CreatePendingEmailInvitationAsync(joinerEmail);

        const string resetPasswordKey = "2.reset-password-key";
        var response = await joinerClient.PostAsJsonAsync(
            "/organizations/users/invite-link/accept",
            new AcceptOrganizationInviteLinkRequestModel
            {
                OrganizationId = created.OrganizationId,
                Code = created.Code,
                ResetPasswordKey = resetPasswordKey,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var organizationUser = await AssertAcceptedMemberAsync(joinerEmail);
        Assert.Equal(resetPasswordKey, organizationUser.ResetPasswordKey);
    }

    // A Staged membership (SCIM/Directory-Connector provisioned: Email set, no linked UserId) is resolved as
    // an existing membership, but IPolicyRequirementQuery still cannot resolve the target org's policies for
    // it, so target-org policies must still be enforced. Here two-step login is required and the joiner has none.
    [Fact]
    public async Task AcceptInviteLink_WithStagedMembership_WhenOrganizationRequiresTwoFactor_AndJoinerHasNoTwoFactor_IsRejected()
    {
        await EnablePolicyAsync(PolicyType.TwoFactorAuthentication);

        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();
        await CreateStagedMembershipAsync(joinerEmail);

        var response = await AcceptAsync(joinerClient, created);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertStillStagedAsync(joinerEmail);
    }

    // Staged path: with no policies, accepting the invite link links and accepts the staged membership.
    [Fact]
    public async Task AcceptInviteLink_WithStagedMembership_WithValidRequest_ReturnsOk()
    {
        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();
        await CreateStagedMembershipAsync(joinerEmail);

        var response = await AcceptAsync(joinerClient, created);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertAcceptedMemberAsync(joinerEmail);
    }

    // Staged path: a joiner with two-step login may accept the invite link into a 2FA org.
    [Fact]
    public async Task AcceptInviteLink_WithStagedMembership_WhenOrganizationRequiresTwoFactor_AndJoinerHasTwoFactor_Succeeds()
    {
        await EnablePolicyAsync(PolicyType.TwoFactorAuthentication);

        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();
        await SetJoinerTwoFactorEnabledAsync(joinerEmail);
        await CreateStagedMembershipAsync(joinerEmail);

        var response = await AcceptAsync(joinerClient, created);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertAcceptedMemberAsync(joinerEmail);
    }

    // Staged path: Single Org must still be enforced against the target organization.
    [Fact]
    public async Task AcceptInviteLink_WithStagedMembership_WhenTargetOrganizationEnforcesSingleOrg_AndJoinerBelongsToAnotherOrg_IsRejected()
    {
        await EnablePolicyAsync(PolicyType.SingleOrg);

        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();
        await AddJoinerToAnotherOrganizationAsync(joinerEmail);
        await CreateStagedMembershipAsync(joinerEmail);

        var response = await AcceptAsync(joinerClient, created);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertStillStagedAsync(joinerEmail);
    }

    // Staged path: Auto-Confirm must still be enforced against the target organization.
    [Fact]
    public async Task AcceptInviteLink_WithStagedMembership_WhenTargetOrganizationEnforcesAutoConfirm_AndJoinerBelongsToAnotherOrg_IsRejected()
    {
        await EnablePolicyAsync(PolicyType.AutomaticUserConfirmation);

        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();
        await AddJoinerToAnotherOrganizationAsync(joinerEmail);
        await CreateStagedMembershipAsync(joinerEmail);

        var response = await AcceptAsync(joinerClient, created);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertStillStagedAsync(joinerEmail);
    }

    // Staged path: account-recovery auto-enroll still enrolls the user (the check runs for all branches).
    [Fact]
    public async Task AcceptInviteLink_WithStagedMembership_AndAccountRecoveryAutoEnrollEnabled_EnrollsUserInAccountRecovery()
    {
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        _organization.UseResetPassword = true;
        _organization.UsePolicies = true;
        await organizationRepository.ReplaceAsync(_organization);

        var policyRepository = _factory.GetService<IPolicyRepository>();
        var resetPasswordPolicy = new Policy
        {
            OrganizationId = _organization.Id,
            Type = PolicyType.ResetPassword,
            Enabled = true,
        };
        resetPasswordPolicy.SetDataModel(new ResetPasswordDataModel { AutoEnrollEnabled = true });
        await policyRepository.CreateAsync(resetPasswordPolicy);

        var created = await CreateInviteLinkAsync();
        var (joinerEmail, joinerClient) = await RegisterAndLoginJoinerAsync();
        await CreateStagedMembershipAsync(joinerEmail);

        const string resetPasswordKey = "2.reset-password-key";
        var response = await joinerClient.PostAsJsonAsync(
            "/organizations/users/invite-link/accept",
            new AcceptOrganizationInviteLinkRequestModel
            {
                OrganizationId = created.OrganizationId,
                Code = created.Code,
                ResetPasswordKey = resetPasswordKey,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var organizationUser = await AssertAcceptedMemberAsync(joinerEmail);
        Assert.Equal(resetPasswordKey, organizationUser.ResetPasswordKey);
    }

    private static Task<HttpResponseMessage> AcceptAsync(HttpClient client, OrganizationInviteLinkResponseModel created) =>
        client.PostAsJsonAsync(
            "/organizations/users/invite-link/accept",
            new AcceptOrganizationInviteLinkRequestModel { OrganizationId = created.OrganizationId, Code = created.Code });

    private async Task EnablePolicyAsync(PolicyType policyType)
    {
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        _organization.UsePolicies = true;
        await organizationRepository.ReplaceAsync(_organization);

        var policyRepository = _factory.GetService<IPolicyRepository>();
        await policyRepository.CreateAsync(new Policy
        {
            OrganizationId = _organization.Id,
            Type = policyType,
            Enabled = true,
        });
    }

    private async Task<OrganizationInviteLinkResponseModel> CreateInviteLinkAsync()
    {
        var createResponse = await _client.PostAsJsonAsync(
            $"/organizations/{_organization.Id}/invite-link",
            new CreateOrganizationInviteLinkRequestModel
            {
                AllowedDomains = ["example.com"],
                Invite = _invite,
                SupportsConfirmation = false,
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<OrganizationInviteLinkResponseModel>();
        Assert.NotNull(created);
        return created;
    }

    private async Task<(string Email, HttpClient Client)> RegisterAndLoginJoinerAsync()
    {
        var joinerEmail = $"integration-test{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(joinerEmail);
        var joinerClient = _factory.CreateClient();
        await new LoginHelper(_factory, joinerClient).LoginAsync(joinerEmail);
        return (joinerEmail, joinerClient);
    }

    // Creates a separate organization and adds the joiner to it as a confirmed member, so that the target
    // organization's single-org / auto-confirm policies have another membership to conflict with. Optionally
    // enables a policy on that other organization to exercise cross-org enforcement.
    private async Task AddJoinerToAnotherOrganizationAsync(string joinerEmail, PolicyType? enablePolicy = null)
    {
        var otherOwnerEmail = $"integration-test{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(otherOwnerEmail);
        var (otherOrganization, _) = await OrganizationTestHelpers.SignUpAsync(
            _factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: otherOwnerEmail,
            passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        if (enablePolicy is not null)
        {
            var organizationRepository = _factory.GetService<IOrganizationRepository>();
            otherOrganization.UsePolicies = true;
            await organizationRepository.ReplaceAsync(otherOrganization);

            var policyRepository = _factory.GetService<IPolicyRepository>();
            await policyRepository.CreateAsync(new Policy
            {
                OrganizationId = otherOrganization.Id,
                Type = enablePolicy.Value,
                Enabled = true,
            });
        }

        await OrganizationTestHelpers.CreateUserAsync(
            _factory,
            otherOrganization.Id,
            joinerEmail,
            OrganizationUserType.User,
            userStatusType: OrganizationUserStatusType.Confirmed);
    }

    // Creates a pending email invitation (Invited, no linked UserId) so the accept resolves an existing
    // membership and takes the AcceptExistingInviteAsync path.
    private async Task CreatePendingEmailInvitationAsync(string joinerEmail)
    {
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = _organization.Id,
            UserId = null,
            Email = joinerEmail,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Invited,
        });
    }

    // Creates a Staged membership (Email set, no linked UserId), mirroring SCIM/Directory-Connector
    // provisioning, so the accept resolves it as an existing membership.
    private async Task CreateStagedMembershipAsync(string joinerEmail)
    {
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = _organization.Id,
            UserId = null,
            Email = joinerEmail,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Staged,
        });
    }

    private async Task SetJoinerTwoFactorEnabledAsync(string joinerEmail)
    {
        var userRepository = _factory.GetService<IUserRepository>();
        await TwoFactorIntegrationTestHelpers.SetUserTwoFactorProvidersJsonAsync(
            userRepository,
            joinerEmail,
            TwoFactorIntegrationTestHelpers.BuildAuthenticatorProvidersJson(TwoFactorIntegrationTestHelpers.AuthenticatorKey));
    }

    private async Task AssertNotMemberAsync(string joinerEmail)
    {
        var userRepository = _factory.GetService<IUserRepository>();
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var joiner = await userRepository.GetByEmailAsync(joinerEmail);
        Assert.NotNull(joiner);

        var organizationUser = await organizationUserRepository.GetByOrganizationAsync(_organization.Id, joiner.Id);
        Assert.Null(organizationUser);
    }

    private async Task<OrganizationUser> AssertAcceptedMemberAsync(string joinerEmail)
    {
        var userRepository = _factory.GetService<IUserRepository>();
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var joiner = await userRepository.GetByEmailAsync(joinerEmail);
        Assert.NotNull(joiner);

        var organizationUser = await organizationUserRepository.GetByOrganizationAsync(_organization.Id, joiner.Id);
        Assert.NotNull(organizationUser);
        Assert.Equal(OrganizationUserStatusType.Accepted, organizationUser.Status);
        return organizationUser;
    }

    private async Task AssertStillInvitedAsync(string joinerEmail)
    {
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var invited = await organizationUserRepository.GetByOrganizationEmailAsync(_organization.Id, joinerEmail);
        Assert.NotNull(invited);
        Assert.Equal(OrganizationUserStatusType.Invited, invited.Status);
    }

    private async Task AssertStillStagedAsync(string joinerEmail)
    {
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        var staged = await organizationUserRepository.GetByOrganizationEmailAsync(_organization.Id, joinerEmail);
        Assert.NotNull(staged);
        Assert.Equal(OrganizationUserStatusType.Staged, staged.Status);
    }
}
