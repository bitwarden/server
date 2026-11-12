using System.Net;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;
using static Bit.Api.IntegrationTest.Auth.Helpers.MasterPasswordPayloads;

namespace Bit.Api.IntegrationTest.Auth.MasterPassword;

public class KdfChangeTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IPasswordHasher<User> _passwordHasher;

    private string _ownerEmail = null!;

    public KdfChangeTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _pushNotificationService = _factory.GetService<IPushNotificationService>();
        _passwordHasher = _factory.GetService<IPasswordHasher<User>>();
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
    [BitAutoData(KdfType.PBKDF2_SHA256, 600001, null, null)]
    [BitAutoData(KdfType.Argon2id, 4, 65, 5)]
    public async Task PostKdf_ValidRequest_SuccessSyncAndLogoutWithReason(KdfType kdf,
        int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        var userBeforeKdfChange = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(userBeforeKdfChange);

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
        Assert.Equal(MasterKeyWrappedUserKey, user.Key);
        Assert.NotNull(user.LastKdfChangeDate);
        Assert.True(user.LastKdfChangeDate > DateTime.UtcNow.AddMinutes(-1));
        Assert.True(user.RevisionDate > DateTime.UtcNow.AddMinutes(-1));
        Assert.True(user.AccountRevisionDate > DateTime.UtcNow.AddMinutes(-1));
        Assert.Equal(userBeforeKdfChange.SecurityStamp, user.SecurityStamp);
        Assert.Equal(PasswordVerificationResult.Success,
            _passwordHasher.VerifyHashedPassword(user, user.MasterPassword!, NewMasterPasswordHash));

        // Validate push notification
        await _pushNotificationService.Received(1)
            .PushAsync(Arg.Is<PushNotification<LogOutPushNotification>>(n => n.Type == PushType.LogOut && n.TargetId == user.Id && n.Payload.Reason == PushNotificationLogOutReason.KdfChange));
        await _pushNotificationService.Received(1)
            .PushAsync(Arg.Is<PushNotification<UserPushNotification>>(n => n.Type == PushType.SyncSettings && n.TargetId == user.Id));
    }

    [Fact]
    public async Task PostKdf_Unauthorized_ReturnsUnauthorized()
    {
        // Don't call LoginAsync to test unauthorized access

        var response = await PostKdfWithKdfRequestAsync(DefaultKdfRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("authenticationData")]
    [InlineData("unlockData")]
    [InlineData("masterPasswordHash")]
    public async Task PostKdf_MissingRequiredJsonKey_BadRequest(string fieldToOmit)
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var payload = new Dictionary<string, object?>
        {
            ["masterPasswordHash"] = MasterPasswordHash,
            ["authenticationData"] = BuildAuthData(DefaultKdfRequest, _ownerEmail),
            ["unlockData"] = BuildUnlockData(DefaultKdfRequest, _ownerEmail)
        };
        payload.Remove(fieldToOmit);

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/kdf");
        message.Content = JsonContent.Create(payload);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains($"missing required properties including: '{fieldToOmit}'", content);
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
            : BuildAuthData(DefaultKdfRequest, _ownerEmail);

        var unlockData = unlockDataNull
            ? null
            : BuildUnlockData(DefaultKdfRequest, _ownerEmail);

        var response = await PostKdfAsync(authenticationData, unlockData);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        if (authenticationDataNull)
        {
            Assert.Contains("The AuthenticationData field is required.", content);
        }
        if (unlockDataNull)
        {
            Assert.Contains("The UnlockData field is required.", content);
        }
    }

    [Fact]
    public async Task PostKdf_InvalidMasterPasswordHash_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var authenticationData = BuildAuthData(DefaultKdfRequest, _ownerEmail);
        var unlockData = BuildUnlockData(DefaultKdfRequest, _ownerEmail);

        var requestModel = new PasswordRequestModel
        {
            MasterPasswordHash = "wrong-master-password-hash",
            NewMasterPasswordHash = NewMasterPasswordHash,
            Key = MasterKeyWrappedUserKey,
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

        var authenticationData = BuildAuthData(DefaultKdfRequest, "wrong-salt@bitwarden.com");
        var unlockData = BuildUnlockData(DefaultKdfRequest, _ownerEmail);

        var response = await PostKdfAsync(authenticationData, unlockData);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid master password salt.", content);
    }

    [Fact]
    public async Task PostKdf_ChangedSaltInUnlockData_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var authenticationData = BuildAuthData(DefaultKdfRequest, _ownerEmail);
        var unlockData = BuildUnlockData(DefaultKdfRequest, "wrong-salt@bitwarden.com");

        var response = await PostKdfAsync(authenticationData, unlockData);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid master password salt.", content);
    }

    [Fact]
    public async Task PostKdf_KdfNotMatching_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var authenticationData = BuildAuthData(DefaultKdfRequest, _ownerEmail);
        var unlockData = BuildUnlockData(
            new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600_001 },
            _ownerEmail);

        var response = await PostKdfAsync(authenticationData, unlockData);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("AuthenticationData and UnlockData must have the same KDF configuration.", content);
    }

    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 1, null, null, "KDF iterations must be between")]
    [InlineData(KdfType.Argon2id, 4, null, 5, "Argon2 memory must be between")]
    [InlineData(KdfType.Argon2id, 4, 65, null, "Argon2 parallelism must be between")]
    public async Task PostKdf_InvalidKdf_BadRequest(KdfType kdf, int kdfIterations, int? kdfMemory, int? kdfParallelism, string expectedError)
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
        Assert.Contains(expectedError, content);
    }

    [Fact]
    public async Task PostKdf_InvalidNewMasterPassword_BadRequest()
    {
        var newMasterPasswordHash = "too-short";

        await _loginHelper.LoginAsync(_ownerEmail);

        var authenticationData = new MasterPasswordAuthenticationDataRequestModel
        {
            Kdf = DefaultKdfRequest,
            MasterPasswordAuthenticationHash = newMasterPasswordHash,
            Salt = _ownerEmail
        };

        var unlockData = new MasterPasswordUnlockDataRequestModel
        {
            Kdf = DefaultKdfRequest,
            MasterKeyWrappedUserKey = MasterKeyWrappedUserKey,
            Salt = _ownerEmail
        };

        var requestModel = new PasswordRequestModel
        {
            MasterPasswordHash = MasterPasswordHash,
            NewMasterPasswordHash = newMasterPasswordHash,
            Key = MasterKeyWrappedUserKey,
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
        var authenticationData = BuildAuthData(kdfRequest, _ownerEmail);
        var unlockData = BuildUnlockData(kdfRequest, _ownerEmail);

        return await PostKdfAsync(authenticationData, unlockData);
    }

    private async Task<HttpResponseMessage> PostKdfAsync(
        MasterPasswordAuthenticationDataRequestModel? authenticationDataRequest,
        MasterPasswordUnlockDataRequestModel? unlockDataRequest)
    {
        var requestModel = new PasswordRequestModel
        {
            MasterPasswordHash = MasterPasswordHash,
            NewMasterPasswordHash = NewMasterPasswordHash,
            Key = MasterKeyWrappedUserKey,
            AuthenticationData = authenticationDataRequest,
            UnlockData = unlockDataRequest
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/kdf");
        message.Content = JsonContent.Create(requestModel);
        return await _client.SendAsync(message);
    }
}
