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
    [BitAutoData(true, true)]
    [BitAutoData(true, false)]
    [BitAutoData(false, true)]
    public async Task RegenerateKeysAsync_UserInOrgOrHasDesignatedEmergencyAccess_ThrowsBadRequest(
        bool inOrganization,
        bool hasDesignatedEmergencyAccess,
        KeyRegenerationRequestModel request)
    {
        if (inOrganization)
        {
            await OrganizationTestHelpers.SignUpAsync(_factory,
                PlanType.EnterpriseAnnually, _ownerEmail, passwordManagerSeats: 10,
                paymentMethod: PaymentMethodType.Card);
        }

        if (hasDesignatedEmergencyAccess)
        {
            await CreateDesignatedEmergencyAccessAsync();
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

    private async Task CreateDesignatedEmergencyAccessAsync()
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
            Status = EmergencyAccessStatusType.Confirmed,
            Type = EmergencyAccessType.View,
            WaitTimeDays = 10,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow
        };
        await _emergencyAccessRepository.CreateAsync(emergencyAccess);
    }
}
