using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.IntegrationTest.KeyManagement.Controllers;

public class AccountsKeyManagementControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private static readonly string _mockEncryptedString =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private readonly HttpClient _client;
    private readonly IEmergencyAccessRepository _emergencyAccessRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private string _ownerEmail = null!;

    public AccountsKeyManagementControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.UpdateConfiguration("globalSettings:launchDarkly:flagValues:pm-12241-private-key-regeneration",
            "true");
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _emergencyAccessRepository = _factory.GetService<IEmergencyAccessRepository>();
        _organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
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

    [Theory]
    [BitAutoData]
    public async Task RegenerateKeysAsync_FeatureFlagTurnedOff_NotFound(KeyRegenerationRequestModel request)
    {
        // Localize factory to inject a false value for the feature flag.
        var localFactory = new ApiApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:launchDarkly:flagValues:pm-12241-private-key-regeneration",
            "false");
        var localClient = localFactory.CreateClient();
        var localEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        var localLoginHelper = new LoginHelper(localFactory, localClient);
        await localFactory.LoginWithNewAccount(localEmail);
        await localLoginHelper.LoginAsync(localEmail);

        request.UserKeyEncryptedUserPrivateKey = _mockEncryptedString;

        var response = await localClient.PostAsJsonAsync("/accounts/key-management/regenerate-keys", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [BitAutoData]
    public async Task RegenerateKeysAsync_NotLoggedIn_Unauthorized(KeyRegenerationRequestModel request)
    {
        request.UserKeyEncryptedUserPrivateKey = _mockEncryptedString;

        var response = await _client.PostAsJsonAsync("/accounts/key-management/regenerate-keys", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Confirmed, EmergencyAccessStatusType.Confirmed)]
    [BitAutoData(OrganizationUserStatusType.Confirmed, EmergencyAccessStatusType.RecoveryApproved)]
    [BitAutoData(OrganizationUserStatusType.Confirmed, EmergencyAccessStatusType.RecoveryInitiated)]
    [BitAutoData(OrganizationUserStatusType.Revoked, EmergencyAccessStatusType.Confirmed)]
    [BitAutoData(OrganizationUserStatusType.Revoked, EmergencyAccessStatusType.RecoveryApproved)]
    [BitAutoData(OrganizationUserStatusType.Revoked, EmergencyAccessStatusType.RecoveryInitiated)]
    [BitAutoData(OrganizationUserStatusType.Confirmed, null)]
    [BitAutoData(OrganizationUserStatusType.Revoked, null)]
    [BitAutoData(OrganizationUserStatusType.Invited, EmergencyAccessStatusType.Confirmed)]
    [BitAutoData(OrganizationUserStatusType.Invited, EmergencyAccessStatusType.RecoveryApproved)]
    [BitAutoData(OrganizationUserStatusType.Invited, EmergencyAccessStatusType.RecoveryInitiated)]
    public async Task RegenerateKeysAsync_UserInOrgOrHasDesignatedEmergencyAccess_ThrowsBadRequest(
        OrganizationUserStatusType organizationUserStatus,
        EmergencyAccessStatusType? emergencyAccessStatus,
        KeyRegenerationRequestModel request)
    {
        if (organizationUserStatus is OrganizationUserStatusType.Confirmed or OrganizationUserStatusType.Revoked)
        {
            await CreateOrganizationUserAsync(organizationUserStatus);
        }

        if (emergencyAccessStatus != null)
        {
            await CreateDesignatedEmergencyAccessAsync(emergencyAccessStatus.Value);
        }

        await _loginHelper.LoginAsync(_ownerEmail);
        request.UserKeyEncryptedUserPrivateKey = _mockEncryptedString;

        var response = await _client.PostAsJsonAsync("/accounts/key-management/regenerate-keys", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [BitAutoData]
    public async Task RegenerateKeysAsync_Success(KeyRegenerationRequestModel request)
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        request.UserKeyEncryptedUserPrivateKey = _mockEncryptedString;

        var response = await _client.PostAsJsonAsync("/accounts/key-management/regenerate-keys", request);
        response.EnsureSuccessStatusCode();

        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);
        Assert.Equal(request.UserPublicKey, user.PublicKey);
        Assert.Equal(request.UserKeyEncryptedUserPrivateKey, user.PrivateKey);
    }

    private async Task CreateOrganizationUserAsync(OrganizationUserStatusType organizationUserStatus)
    {
        var (_, organizationUser) = await OrganizationTestHelpers.SignUpAsync(_factory,
            PlanType.EnterpriseAnnually, _ownerEmail, passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);
        organizationUser.Status = organizationUserStatus;
        await _organizationUserRepository.ReplaceAsync(organizationUser);
    }

    private async Task CreateDesignatedEmergencyAccessAsync(EmergencyAccessStatusType emergencyAccessStatus)
    {
        var tempEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(tempEmail);

        var tempUser = await _userRepository.GetByEmailAsync(tempEmail);
        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        var emergencyAccess = new EmergencyAccess
        {
            GrantorId = tempUser!.Id,
            GranteeId = user!.Id,
            KeyEncrypted = _mockEncryptedString,
            Status = emergencyAccessStatus,
            Type = EmergencyAccessType.View,
            WaitTimeDays = 10,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow
        };
        await _emergencyAccessRepository.CreateAsync(emergencyAccess);
    }
}
