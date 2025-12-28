using System.Net;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.Controllers;

public class AccountsControllerTest : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private static readonly string _masterKeyWrappedUserKey =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private static readonly string _masterPasswordHash = "master_password_hash";
    private static readonly string _newMasterPasswordHash = "new_master_password_hash";

    private static readonly KdfRequestModel _defaultKdfRequest =
        new() { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600_000 };

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IFeatureService _featureService;
    private readonly IPasswordHasher<User> _passwordHasher;

    private string _ownerEmail = null!;

    public AccountsControllerTest(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _factory.SubstituteService<IFeatureService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _pushNotificationService = _factory.GetService<IPushNotificationService>();
        _featureService = _factory.GetService<IFeatureService>();
        _passwordHasher = _factory.GetService<IPasswordHasher<User>>();
    }

    public async ValueTask InitializeAsync()
    {
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetAccountsProfile_success()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        using var message = new HttpRequestMessage(HttpMethod.Get, "/accounts/profile");
        var response = await _client.SendAsync(message);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<ProfileResponseModel>();
        Assert.NotNull(content);
        Assert.Equal(_ownerEmail, content.Email);
        Assert.NotNull(content.Name);
        Assert.True(content.EmailVerified);
        Assert.False(content.Premium);
        Assert.False(content.PremiumFromOrganization);
        Assert.Equal("en-US", content.Culture);
        Assert.NotNull(content.Key);
        Assert.NotNull(content.PrivateKey);
        Assert.NotNull(content.SecurityStamp);
    }

    [Theory]
    [BitAutoData(KdfType.PBKDF2_SHA256, 600001, null, null)]
    [BitAutoData(KdfType.Argon2id, 4, 65, 5)]
    public async Task PostKdf_ValidRequestLogoutOnKdfChangeFeatureFlagOff_SuccessLogout(KdfType kdf,
        int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        var userBeforeKdfChange = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(userBeforeKdfChange);

        _featureService.IsEnabled(FeatureFlagKeys.NoLogoutOnKdfChange).Returns(false);

        await _loginHelper.LoginAsync(_ownerEmail);

        var kdfRequest = new KdfRequestModel
        {
            KdfType = kdf,
            Iterations = kdfIterations,
            Memory = kdfMemory,
            Parallelism = kdfParallelism,
        };

        var response = await PostKdfWithKdfRequestAsync(kdfRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Validate that the user fields were updated correctly
        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);
        Assert.Equal(kdfRequest.KdfType, user.Kdf);
        Assert.Equal(kdfRequest.Iterations, user.KdfIterations);
        Assert.Equal(kdfRequest.Memory, user.KdfMemory);
        Assert.Equal(kdfRequest.Parallelism, user.KdfParallelism);
        Assert.Equal(_masterKeyWrappedUserKey, user.Key);
        Assert.NotNull(user.LastKdfChangeDate);
        Assert.True(user.LastKdfChangeDate > DateTime.UtcNow.AddMinutes(-1));
        Assert.True(user.RevisionDate > DateTime.UtcNow.AddMinutes(-1));
        Assert.True(user.AccountRevisionDate > DateTime.UtcNow.AddMinutes(-1));
        Assert.NotEqual(userBeforeKdfChange.SecurityStamp, user.SecurityStamp);
        Assert.Equal(PasswordVerificationResult.Success,
            _passwordHasher.VerifyHashedPassword(user, user.MasterPassword!, _newMasterPasswordHash));

        // Validate push notification
        await _pushNotificationService.Received(1).PushLogOutAsync(user.Id);
    }

    [Theory]
    [BitAutoData(KdfType.PBKDF2_SHA256, 600001, null, null)]
    [BitAutoData(KdfType.Argon2id, 4, 65, 5)]
    public async Task PostKdf_ValidRequestLogoutOnKdfChangeFeatureFlagOn_SuccessSyncAndLogoutWithReason(KdfType kdf,
        int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        var userBeforeKdfChange = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(userBeforeKdfChange);

        _featureService.IsEnabled(FeatureFlagKeys.NoLogoutOnKdfChange).Returns(true);

        await _loginHelper.LoginAsync(_ownerEmail);

        var kdfRequest = new KdfRequestModel
        {
            KdfType = kdf,
            Iterations = kdfIterations,
            Memory = kdfMemory,
            Parallelism = kdfParallelism,
        };

        var response = await PostKdfWithKdfRequestAsync(kdfRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Validate that the user fields were updated correctly
        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);
        Assert.Equal(kdfRequest.KdfType, user.Kdf);
        Assert.Equal(kdfRequest.Iterations, user.KdfIterations);
        Assert.Equal(kdfRequest.Memory, user.KdfMemory);
        Assert.Equal(kdfRequest.Parallelism, user.KdfParallelism);
        Assert.Equal(_masterKeyWrappedUserKey, user.Key);
        Assert.NotNull(user.LastKdfChangeDate);
        Assert.True(user.LastKdfChangeDate > DateTime.UtcNow.AddMinutes(-1));
        Assert.True(user.RevisionDate > DateTime.UtcNow.AddMinutes(-1));
        Assert.True(user.AccountRevisionDate > DateTime.UtcNow.AddMinutes(-1));
        Assert.Equal(userBeforeKdfChange.SecurityStamp, user.SecurityStamp);
        Assert.Equal(PasswordVerificationResult.Success,
            _passwordHasher.VerifyHashedPassword(user, user.MasterPassword!, _newMasterPasswordHash));

        // Validate push notification
        await _pushNotificationService.Received(1)
            .PushLogOutAsync(user.Id, false, PushNotificationLogOutReason.KdfChange);
        await _pushNotificationService.Received(1).PushSyncSettingsAsync(user.Id);
    }

    [Fact]
    public async Task PostKdf_Unauthorized_ReturnsUnauthorized()
    {
        // Don't call LoginAsync to test unauthorized access

        var response = await PostKdfWithKdfRequestAsync(_defaultKdfRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PostKdf_AuthenticationDataOrUnlockDataNull_BadRequest(bool authenticationDataNull,
        bool unlockDataNull)
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var authenticationData = authenticationDataNull
            ? null
            : new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = _defaultKdfRequest,
                MasterPasswordAuthenticationHash = _newMasterPasswordHash,
                Salt = _ownerEmail
            };

        var unlockData = unlockDataNull
            ? null
            : new MasterPasswordUnlockDataRequestModel
            {
                Kdf = _defaultKdfRequest,
                MasterKeyWrappedUserKey = _masterKeyWrappedUserKey,
                Salt = _ownerEmail
            };

        var response = await PostKdfAsync(authenticationData, unlockData);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("AuthenticationData and UnlockData must be provided.", content);
    }

    [Fact]
    public async Task PostKdf_InvalidMasterPasswordHash_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var authenticationData = new MasterPasswordAuthenticationDataRequestModel
        {
            Kdf = _defaultKdfRequest,
            MasterPasswordAuthenticationHash = _newMasterPasswordHash,
            Salt = _ownerEmail
        };

        var unlockData = new MasterPasswordUnlockDataRequestModel
        {
            Kdf = _defaultKdfRequest,
            MasterKeyWrappedUserKey = _masterKeyWrappedUserKey,
            Salt = _ownerEmail
        };

        var requestModel = new PasswordRequestModel
        {
            MasterPasswordHash = "wrong-master-password-hash",
            NewMasterPasswordHash = _newMasterPasswordHash,
            Key = _masterKeyWrappedUserKey,
            AuthenticationData = authenticationData,
            UnlockData = unlockData
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/kdf");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Incorrect password", content);
    }

    [Fact]
    public async Task PostKdf_ChangedSaltInAuthenticationData_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var authenticationData = new MasterPasswordAuthenticationDataRequestModel
        {
            Kdf = _defaultKdfRequest,
            MasterPasswordAuthenticationHash = _newMasterPasswordHash,
            Salt = "wrong-salt@bitwarden.com"
        };

        var unlockData = new MasterPasswordUnlockDataRequestModel
        {
            Kdf = _defaultKdfRequest,
            MasterKeyWrappedUserKey = _masterKeyWrappedUserKey,
            Salt = _ownerEmail
        };

        var response = await PostKdfAsync(authenticationData, unlockData);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid master password salt.", content);
    }

    [Fact]
    public async Task PostKdf_ChangedSaltInUnlockData_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var authenticationData = new MasterPasswordAuthenticationDataRequestModel
        {
            Kdf = _defaultKdfRequest,
            MasterPasswordAuthenticationHash = _newMasterPasswordHash,
            Salt = _ownerEmail
        };

        var unlockData = new MasterPasswordUnlockDataRequestModel
        {
            Kdf = _defaultKdfRequest,
            MasterKeyWrappedUserKey = _masterKeyWrappedUserKey,
            Salt = "wrong-salt@bitwarden.com"
        };

        var response = await PostKdfAsync(authenticationData, unlockData);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid master password salt.", content);
    }

    [Fact]
    public async Task PostKdf_KdfNotMatching_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var authenticationData = new MasterPasswordAuthenticationDataRequestModel
        {
            Kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600_000 },
            MasterPasswordAuthenticationHash = _newMasterPasswordHash,
            Salt = _ownerEmail
        };

        var unlockData = new MasterPasswordUnlockDataRequestModel
        {
            Kdf = new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600_001 },
            MasterKeyWrappedUserKey = _masterKeyWrappedUserKey,
            Salt = _ownerEmail
        };

        var response = await PostKdfAsync(authenticationData, unlockData);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("KDF settings must be equal for authentication and unlock.", content);
    }

    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 1, null, null)]
    [InlineData(KdfType.Argon2id, 4, null, 5)]
    [InlineData(KdfType.Argon2id, 4, 65, null)]
    public async Task PostKdf_InvalidKdf_BadRequest(KdfType kdf, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var kdfRequest = new KdfRequestModel
        {
            KdfType = kdf,
            Iterations = kdfIterations,
            Memory = kdfMemory,
            Parallelism = kdfParallelism
        };

        var response = await PostKdfWithKdfRequestAsync(kdfRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("KDF settings are invalid", content);
    }

    [Fact]
    public async Task PostKdf_InvalidNewMasterPassword_BadRequest()
    {
        var newMasterPasswordHash = "too-short";

        await _loginHelper.LoginAsync(_ownerEmail);

        var authenticationData = new MasterPasswordAuthenticationDataRequestModel
        {
            Kdf = _defaultKdfRequest,
            MasterPasswordAuthenticationHash = newMasterPasswordHash,
            Salt = _ownerEmail
        };

        var unlockData = new MasterPasswordUnlockDataRequestModel
        {
            Kdf = _defaultKdfRequest,
            MasterKeyWrappedUserKey = _masterKeyWrappedUserKey,
            Salt = _ownerEmail
        };

        var requestModel = new PasswordRequestModel
        {
            MasterPasswordHash = _masterPasswordHash,
            NewMasterPasswordHash = newMasterPasswordHash,
            Key = _masterKeyWrappedUserKey,
            AuthenticationData = authenticationData,
            UnlockData = unlockData
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/kdf");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Passwords must be at least", content);
    }

    private async Task<HttpResponseMessage> PostKdfWithKdfRequestAsync(KdfRequestModel kdfRequest)
    {
        var authenticationData = new MasterPasswordAuthenticationDataRequestModel
        {
            Kdf = kdfRequest,
            MasterPasswordAuthenticationHash = _newMasterPasswordHash,
            Salt = _ownerEmail
        };

        var unlockData = new MasterPasswordUnlockDataRequestModel
        {
            Kdf = kdfRequest,
            MasterKeyWrappedUserKey = _masterKeyWrappedUserKey,
            Salt = _ownerEmail
        };

        return await PostKdfAsync(authenticationData, unlockData);
    }

    private async Task<HttpResponseMessage> PostKdfAsync(
        MasterPasswordAuthenticationDataRequestModel? authenticationDataRequest,
        MasterPasswordUnlockDataRequestModel? unlockDataRequest)
    {
        var requestModel = new PasswordRequestModel
        {
            MasterPasswordHash = _masterPasswordHash,
            NewMasterPasswordHash = _newMasterPasswordHash,
            Key = _masterKeyWrappedUserKey,
            AuthenticationData = authenticationDataRequest,
            UnlockData = unlockDataRequest
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/kdf");
        message.Content = JsonContent.Create(requestModel);
        return await _client.SendAsync(message);
    }
}
