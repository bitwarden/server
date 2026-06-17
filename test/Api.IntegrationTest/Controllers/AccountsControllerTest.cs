using System.Net;
using System.Text.Json;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;
using static Bit.Core.KeyManagement.Enums.SignatureAlgorithm;

namespace Bit.Api.IntegrationTest.Controllers;

public class AccountsControllerTest : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private static readonly string _masterKeyWrappedUserKey =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";
    private static readonly string _mockEncryptedType7String = "7.AOs41Hd8OQiCPXjyJKCiDA==";
    private static readonly string _mockEncryptedType7WrappedSigningKey = "7.DRv74Kg1RSlFSam1MNFlGD==";

    private static readonly string _masterPasswordHash = "master_password_hash";
    private static readonly string _newMasterPasswordHash = "new_master_password_hash";

    private static readonly KdfRequestModel _defaultKdfRequest =
        new() { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600_000 };

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IMailService _mailService;
    private readonly IFeatureService _featureService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly IUserSignatureKeyPairRepository _userSignatureKeyPairRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IStripeSyncService _stripeSyncService;

    private string _ownerEmail = null!;

    public AccountsControllerTest(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _factory.SubstituteService<IFeatureService>(_ => { });
        _factory.SubstituteService<IStripeSyncService>(_ => { });
        _factory.SubstituteService<IMailService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _pushNotificationService = _factory.GetService<IPushNotificationService>();
        _mailService = _factory.GetService<IMailService>();
        _featureService = _factory.GetService<IFeatureService>();
        _passwordHasher = _factory.GetService<IPasswordHasher<User>>();
        _organizationRepository = _factory.GetService<IOrganizationRepository>();
        _ssoConfigRepository = _factory.GetService<ISsoConfigRepository>();
        _userSignatureKeyPairRepository = _factory.GetService<IUserSignatureKeyPairRepository>();
        _eventRepository = _factory.GetService<IEventRepository>();
        _organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        _stripeSyncService = _factory.GetService<IStripeSyncService>();
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

    // Builders for the dual-payload auth + unlock blocks. Tests vary KDF and
    // salt; everything else is constant. Shared across both the change-KDF and
    // V2 password-modification tests in this file.
    private static MasterPasswordAuthenticationDataRequestModel BuildAuthData(KdfRequestModel kdf, string salt) =>
        new() { Kdf = kdf, MasterPasswordAuthenticationHash = _newMasterPasswordHash, Salt = salt };

    private static MasterPasswordUnlockDataRequestModel BuildUnlockData(KdfRequestModel kdf, string salt) =>
        new() { Kdf = kdf, MasterKeyWrappedUserKey = _masterKeyWrappedUserKey, Salt = salt };

    // Builds an (auth, unlock) pair where one side is perturbed so the agreement
    // validator must fire. Used by the V2 *_MismatchedKdfOrSalt_BadRequest tests.
    //   mismatchKind:  "kdf" | "salt"  — which field disagrees
    //   perturbedSide: "auth" | "unlock" — which half carries the bad value
    private (MasterPasswordAuthenticationDataRequestModel auth, MasterPasswordUnlockDataRequestModel unlock)
        BuildMismatchedAuthAndUnlock(string mismatchKind, string perturbedSide)
    {
        var perturbedKdf = mismatchKind == "kdf"
            ? new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 700_000 }
            : _defaultKdfRequest;
        var perturbedSalt = mismatchKind == "salt" ? "different-salt@bitwarden.com" : _ownerEmail;

        return perturbedSide == "auth"
            ? (BuildAuthData(perturbedKdf, perturbedSalt), BuildUnlockData(_defaultKdfRequest, _ownerEmail))
            : (BuildAuthData(_defaultKdfRequest, _ownerEmail), BuildUnlockData(perturbedKdf, perturbedSalt));
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
            : BuildAuthData(_defaultKdfRequest, _ownerEmail);

        var unlockData = unlockDataNull
            ? null
            : BuildUnlockData(_defaultKdfRequest, _ownerEmail);

        var response = await PostKdfAsync(authenticationData, unlockData);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("AuthenticationData and UnlockData must be provided.", content);
    }

    [Fact]
    public async Task PostKdf_InvalidMasterPasswordHash_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var authenticationData = BuildAuthData(_defaultKdfRequest, _ownerEmail);
        var unlockData = BuildUnlockData(_defaultKdfRequest, _ownerEmail);

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

        var authenticationData = BuildAuthData(_defaultKdfRequest, "wrong-salt@bitwarden.com");
        var unlockData = BuildUnlockData(_defaultKdfRequest, _ownerEmail);

        var response = await PostKdfAsync(authenticationData, unlockData);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid master password salt.", content);
    }

    [Fact]
    public async Task PostKdf_ChangedSaltInUnlockData_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var authenticationData = BuildAuthData(_defaultKdfRequest, _ownerEmail);
        var unlockData = BuildUnlockData(_defaultKdfRequest, "wrong-salt@bitwarden.com");

        var response = await PostKdfAsync(authenticationData, unlockData);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid master password salt.", content);
    }

    [Fact]
    public async Task PostKdf_KdfNotMatching_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var authenticationData = BuildAuthData(_defaultKdfRequest, _ownerEmail);
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


    /// <summary>
    /// Verifies the dual-payload self-service password change path accepts
    /// legacy-KDF users — those whose stored KDF predates the current minimum.
    /// <c>ValidateKdfAndSaltAgreement</c> must check agreement between
    /// <c>AuthenticationData</c> and <c>UnlockData</c>, not range.
    /// <para>
    /// Scope: end-to-end through the V2 path; also asserts the KDF is left
    /// untouched (a silent server-side bump would corrupt the account, since
    /// the new auth hash was derived client-side against the legacy KDF) and
    /// that other-device logout fires.
    /// </para>
    /// <para>
    /// Note: mutating the KDF columns below does not invalidate the seeded
    /// password — <c>user.MasterPassword</c> is Identity's <c>IPasswordHasher</c>
    /// output over the wire-sent hash, not derived from <c>user.Kdf</c>.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 100_000, null, null)] // plausible legacy: real pre-600k users
    [InlineData(KdfType.PBKDF2_SHA256, 5_000, null, null)]   // far below minimum: no soft floor either
    [InlineData(KdfType.Argon2id, 2, 14, 4)]                 // barely sub-minimum: 1mb below memory floor
    [InlineData(KdfType.Argon2id, 1, 8, 1)]                  // far below minimum: no Argon2-specific gate
    public async Task PostPassword_V2_LegacyKdfBelowMinimum_Success(
        KdfType kdf, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        // Arrange: downgrade the seeded user's KDF to a sub-minimum value to
        // simulate a real legacy-KDF account that predates the current floor.
        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);
        user.Kdf = kdf;
        user.KdfIterations = kdfIterations;
        user.KdfMemory = kdfMemory;
        user.KdfParallelism = kdfParallelism;
        await _userRepository.ReplaceAsync(user);

        await _loginHelper.LoginAsync(_ownerEmail);

        // Both halves of the V2 payload echo the user's legacy KDF — agreement,
        // not range, is what ValidateKdfAndSaltAgreement enforces.
        var legacyKdfRequest = new KdfRequestModel
        {
            KdfType = kdf,
            Iterations = kdfIterations,
            Memory = kdfMemory,
            Parallelism = kdfParallelism,
        };

        // Populating AuthenticationData + UnlockData routes the controller to
        // the new SelfServicePasswordChangeCommand (V2) path; the legacy
        // NewMasterPasswordHash / Key fields are accepted but ignored.
        var requestModel = new PasswordRequestModel
        {
            MasterPasswordHash = _masterPasswordHash,
            NewMasterPasswordHash = _newMasterPasswordHash,
            Key = _masterKeyWrappedUserKey,
            AuthenticationData = BuildAuthData(legacyKdfRequest, _ownerEmail),
            UnlockData = BuildUnlockData(legacyKdfRequest, _ownerEmail),
        };

        // Act: hit the real endpoint so model binding, validation, auth filter,
        // command dispatch, and repository write all run end-to-end.
        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        // Surface the error body on failure — a bare EnsureSuccessStatusCode
        // hides the validator message that points at any future range-check regression.
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected success but got {response.StatusCode}. Error: {errorContent}");
        }

        // Assert: new password was persisted (rules out a silent no-op success).
        var updatedUser = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(updatedUser);
        Assert.Equal(PasswordVerificationResult.Success,
            _passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.MasterPassword!, _newMasterPasswordHash));

        // KDF must be unchanged — this flow changes the password only. A silent
        // bump to current minimum would corrupt the account: the new auth hash
        // was derived client-side against the legacy KDF.
        Assert.Equal(kdf, updatedUser.Kdf);
        Assert.Equal(kdfIterations, updatedUser.KdfIterations);
        Assert.Equal(kdfMemory, updatedUser.KdfMemory);
        Assert.Equal(kdfParallelism, updatedUser.KdfParallelism);

        // Other devices are logged out, current session is preserved
        // (excludeCurrentContextFromPush: true) — self-service-specific behavior.
        await _pushNotificationService.Received(1).PushLogOutAsync(updatedUser.Id, true);

        // User_ChangedPassword event was logged.
        var events = await _eventRepository.GetManyByUserAsync(
            updatedUser.Id, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1),
            new PageOptions { PageSize = 100 });
        Assert.Contains(events.Data, e => e.Type == EventType.User_ChangedPassword && e.UserId == updatedUser.Id);
    }

    /// <summary>
    /// Verifies the boundary validator's agreement checks fire on
    /// <c>POST /accounts/password</c>: a mismatched KDF or salt between
    /// <c>AuthenticationData</c> and <c>UnlockData</c> is rejected with 400.
    /// Complements the legacy-KDF success test by proving the agreement
    /// invariant isn't passively letting everything through.
    /// </summary>
    [Theory]
    [InlineData("kdf", "unlock", "AuthenticationData and UnlockData must have the same KDF configuration.")]
    [InlineData("kdf", "auth", "AuthenticationData and UnlockData must have the same KDF configuration.")]
    [InlineData("salt", "unlock", "Invalid master password salt.")]
    [InlineData("salt", "auth", "Invalid master password salt.")]
    public async Task PostPassword_V2_MismatchedKdfOrSalt_BadRequest(
        string mismatchKind, string perturbedSide, string expectedError)
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var (auth, unlock) = BuildMismatchedAuthAndUnlock(mismatchKind, perturbedSide);
        var requestModel = new PasswordRequestModel
        {
            MasterPasswordHash = _masterPasswordHash,
            NewMasterPasswordHash = _newMasterPasswordHash,
            Key = _masterKeyWrappedUserKey,
            AuthenticationData = auth,
            UnlockData = unlock,
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedError, content);
    }

    [Fact]
    public async Task PostPassword_V2_Unauthorized_ReturnsUnauthorized()
    {
        // No LoginAsync — the request is anonymous and must be rejected before
        // it reaches the action body.
        var requestModel = new PasswordRequestModel
        {
            MasterPasswordHash = _masterPasswordHash,
            NewMasterPasswordHash = _newMasterPasswordHash,
            Key = _masterKeyWrappedUserKey,
            AuthenticationData = BuildAuthData(_defaultKdfRequest, _ownerEmail),
            UnlockData = BuildUnlockData(_defaultKdfRequest, _ownerEmail),
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostPassword_V2_InvalidMasterPasswordHash_BadRequest()
    {
        // Self-service change-password verifies the current password before
        // applying the new one. A wrong current hash must be rejected.
        await _loginHelper.LoginAsync(_ownerEmail);

        var requestModel = new PasswordRequestModel
        {
            MasterPasswordHash = "wrong-master-password-hash",
            NewMasterPasswordHash = _newMasterPasswordHash,
            Key = _masterKeyWrappedUserKey,
            AuthenticationData = BuildAuthData(_defaultKdfRequest, _ownerEmail),
            UnlockData = BuildUnlockData(_defaultKdfRequest, _ownerEmail),
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Incorrect password", content);
    }

    /// <summary>
    /// Verifies the dual-payload admin-set-temporary-password replacement path
    /// accepts legacy-KDF users — those whose stored KDF predates the current
    /// minimum. <c>ValidateKdfAndSaltAgreement</c> must check agreement between
    /// <c>AuthenticationData</c> and <c>UnlockData</c>, not range.
    /// <para>
    /// Scope: end-to-end through the V2 path; also asserts the KDF is left
    /// untouched, <c>ForcePasswordReset</c> is cleared, the event is logged,
    /// and logout fires.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 100_000, null, null)] // plausible legacy: real pre-600k users
    [InlineData(KdfType.PBKDF2_SHA256, 5_000, null, null)]   // far below minimum: no soft floor either
    [InlineData(KdfType.Argon2id, 2, 14, 4)]                 // barely sub-minimum: 1mb below memory floor
    [InlineData(KdfType.Argon2id, 1, 8, 1)]                  // far below minimum: no Argon2-specific gate
    public async Task PutUpdateTempPassword_V2_LegacyKdfBelowMinimum_Success(
        KdfType kdf, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        // Arrange: downgrade KDF and set ForcePasswordReset so the command's
        // precondition (admin-set temp password is pending) is satisfied.
        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);
        user.Kdf = kdf;
        user.KdfIterations = kdfIterations;
        user.KdfMemory = kdfMemory;
        user.KdfParallelism = kdfParallelism;
        user.ForcePasswordReset = true;
        await _userRepository.ReplaceAsync(user);

        await _loginHelper.LoginAsync(_ownerEmail);

        var legacyKdfRequest = new KdfRequestModel
        {
            KdfType = kdf,
            Iterations = kdfIterations,
            Memory = kdfMemory,
            Parallelism = kdfParallelism,
        };

        var requestModel = new UpdateTempPasswordRequestModel
        {
            NewMasterPasswordHash = _newMasterPasswordHash,
            Key = _masterKeyWrappedUserKey,
            AuthenticationData = BuildAuthData(legacyKdfRequest, _ownerEmail),
            UnlockData = BuildUnlockData(legacyKdfRequest, _ownerEmail),
        };

        using var message = new HttpRequestMessage(HttpMethod.Put, "/accounts/update-temp-password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected success but got {response.StatusCode}. Error: {errorContent}");
        }

        var updatedUser = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(updatedUser);
        Assert.Equal(PasswordVerificationResult.Success,
            _passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.MasterPassword!, _newMasterPasswordHash));

        // KDF unchanged — this flow swaps the password only.
        Assert.Equal(kdf, updatedUser.Kdf);
        Assert.Equal(kdfIterations, updatedUser.KdfIterations);
        Assert.Equal(kdfMemory, updatedUser.KdfMemory);
        Assert.Equal(kdfParallelism, updatedUser.KdfParallelism);

        // The "temp password pending" flag is cleared once the user replaces it.
        Assert.False(updatedUser.ForcePasswordReset);

        // All sessions (including the current one) are logged out
        // (excludeCurrentContextFromPush: false, the default) — admin-set temp
        // password flow.
        await _pushNotificationService.Received(1).PushLogOutAsync(updatedUser.Id);

        var events = await _eventRepository.GetManyByUserAsync(
            updatedUser.Id, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1),
            new PageOptions { PageSize = 100 });
        Assert.Contains(events.Data, e => e.Type == EventType.User_UpdatedTempPassword && e.UserId == updatedUser.Id);
    }

    /// <summary>
    /// Verifies the boundary validator's agreement checks fire on
    /// <c>PUT /accounts/update-temp-password</c>: a mismatched KDF or salt
    /// between <c>AuthenticationData</c> and <c>UnlockData</c> is rejected
    /// with 400. ForcePasswordReset state is irrelevant — model validation
    /// runs before the command's preconditions.
    /// </summary>
    [Theory]
    [InlineData("kdf", "unlock", "AuthenticationData and UnlockData must have the same KDF configuration.")]
    [InlineData("kdf", "auth", "AuthenticationData and UnlockData must have the same KDF configuration.")]
    [InlineData("salt", "unlock", "Invalid master password salt.")]
    [InlineData("salt", "auth", "Invalid master password salt.")]
    public async Task PutUpdateTempPassword_V2_MismatchedKdfOrSalt_BadRequest(
        string mismatchKind, string perturbedSide, string expectedError)
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var (auth, unlock) = BuildMismatchedAuthAndUnlock(mismatchKind, perturbedSide);
        var requestModel = new UpdateTempPasswordRequestModel
        {
            NewMasterPasswordHash = _newMasterPasswordHash,
            Key = _masterKeyWrappedUserKey,
            AuthenticationData = auth,
            UnlockData = unlock,
        };

        using var message = new HttpRequestMessage(HttpMethod.Put, "/accounts/update-temp-password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedError, content);
    }

    [Fact]
    public async Task PutUpdateTempPassword_V2_Unauthorized_ReturnsUnauthorized()
    {
        // No LoginAsync — anonymous requests must be rejected.
        var requestModel = new UpdateTempPasswordRequestModel
        {
            NewMasterPasswordHash = _newMasterPasswordHash,
            Key = _masterKeyWrappedUserKey,
            AuthenticationData = BuildAuthData(_defaultKdfRequest, _ownerEmail),
            UnlockData = BuildUnlockData(_defaultKdfRequest, _ownerEmail),
        };

        using var message = new HttpRequestMessage(HttpMethod.Put, "/accounts/update-temp-password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutUpdateTempPassword_V2_NoForcePasswordReset_BadRequest()
    {
        // Seeded user has ForcePasswordReset = false (the default) — no
        // admin-set temp password is pending, so the command must reject.
        await _loginHelper.LoginAsync(_ownerEmail);

        var requestModel = new UpdateTempPasswordRequestModel
        {
            NewMasterPasswordHash = _newMasterPasswordHash,
            Key = _masterKeyWrappedUserKey,
            AuthenticationData = BuildAuthData(_defaultKdfRequest, _ownerEmail),
            UnlockData = BuildUnlockData(_defaultKdfRequest, _ownerEmail),
        };

        using var message = new HttpRequestMessage(HttpMethod.Put, "/accounts/update-temp-password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("User does not have a temporary password to update.", content);
    }

    /// <summary>
    /// Verifies <c>PUT /accounts/update-tde-offboarding-password</c> rejects
    /// sub-minimum KDF values. Unlike the password-change / temp-password
    /// flows, TDE offboarding sets a brand-new master password and runs the
    /// full <c>ValidateAuthenticationAndUnlockData</c> validator — range
    /// enforcement is required by design. Model validation fires before the
    /// SSO preconditions, so no org setup is needed.
    /// </summary>
    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 100_000, null, null, "KDF iterations must be between")]
    [InlineData(KdfType.Argon2id, 1, 64, 4, "Argon2 iterations must be between")]
    [InlineData(KdfType.Argon2id, 3, 14, 4, "Argon2 memory must be between")]
    public async Task PutUpdateTdeOffboardingPassword_V2_BelowMinimumKdf_BadRequest(
        KdfType kdf, int kdfIterations, int? kdfMemory, int? kdfParallelism, string expectedError)
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var subMinKdfRequest = new KdfRequestModel
        {
            KdfType = kdf,
            Iterations = kdfIterations,
            Memory = kdfMemory,
            Parallelism = kdfParallelism,
        };

        // Auth and unlock fully agree on salt and KDF — the only validator
        // error is the range check firing on the sub-minimum KDF.
        var requestModel = new UpdateTdeOffboardingPasswordRequestModel
        {
            NewMasterPasswordHash = _newMasterPasswordHash,
            Key = _masterKeyWrappedUserKey,
            AuthenticationData = BuildAuthData(subMinKdfRequest, _ownerEmail),
            UnlockData = BuildUnlockData(subMinKdfRequest, _ownerEmail),
        };

        using var message = new HttpRequestMessage(HttpMethod.Put, "/accounts/update-tde-offboarding-password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedError, content);
    }

    /// <summary>
    /// Verifies the boundary validator's agreement checks fire on
    /// <c>PUT /accounts/update-tde-offboarding-password</c>: a mismatched KDF
    /// or salt between <c>AuthenticationData</c> and <c>UnlockData</c> is
    /// rejected with 400 before any SSO precondition is evaluated.
    /// </summary>
    [Theory]
    [InlineData("kdf", "unlock", "AuthenticationData and UnlockData must have the same KDF configuration.")]
    [InlineData("kdf", "auth", "AuthenticationData and UnlockData must have the same KDF configuration.")]
    [InlineData("salt", "unlock", "Invalid master password salt.")]
    [InlineData("salt", "auth", "Invalid master password salt.")]
    public async Task PutUpdateTdeOffboardingPassword_V2_MismatchedKdfOrSalt_BadRequest(
        string mismatchKind, string perturbedSide, string expectedError)
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var (auth, unlock) = BuildMismatchedAuthAndUnlock(mismatchKind, perturbedSide);
        var requestModel = new UpdateTdeOffboardingPasswordRequestModel
        {
            NewMasterPasswordHash = _newMasterPasswordHash,
            Key = _masterKeyWrappedUserKey,
            AuthenticationData = auth,
            UnlockData = unlock,
        };

        using var message = new HttpRequestMessage(HttpMethod.Put, "/accounts/update-tde-offboarding-password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedError, content);
    }

    [Fact]
    public async Task PutUpdateTdeOffboardingPassword_V2_Unauthorized_ReturnsUnauthorized()
    {
        // No LoginAsync — anonymous requests must be rejected.
        var requestModel = new UpdateTdeOffboardingPasswordRequestModel
        {
            NewMasterPasswordHash = _newMasterPasswordHash,
            Key = _masterKeyWrappedUserKey,
            AuthenticationData = BuildAuthData(_defaultKdfRequest, _ownerEmail),
            UnlockData = BuildUnlockData(_defaultKdfRequest, _ownerEmail),
        };

        using var message = new HttpRequestMessage(HttpMethod.Put, "/accounts/update-tde-offboarding-password");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [BitAutoData]
    public async Task PostSetPasswordAsync_V1_MasterPasswordDecryption_Success(string organizationSsoIdentifier)
    {
        // Arrange - Create organization and user
        var ownerEmail = $"owner-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail);

        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            ownerEmail: ownerEmail,
            name: "Test Org V1");
        organization.UseSso = true;
        organization.Identifier = organizationSsoIdentifier;
        await _organizationRepository.ReplaceAsync(organization);

        await _ssoConfigRepository.CreateAsync(new SsoConfig
        {
            OrganizationId = organization.Id,
            Enabled = true,
            Data = JsonSerializer.Serialize(new SsoConfigurationData
            {
                MemberDecryptionType = MemberDecryptionType.MasterPassword,
            }, JsonHelpers.CamelCase),
        });

        // Create user with password initially, so we can login
        var userEmail = $"user-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(userEmail);

        // Add user to organization
        var user = await _userRepository.GetByEmailAsync(userEmail);
        Assert.NotNull(user);
        await OrganizationTestHelpers.CreateUserAsync(_factory, organization.Id, userEmail,
            OrganizationUserType.User, userStatusType: OrganizationUserStatusType.Invited);

        // Login as the user
        await _loginHelper.LoginAsync(userEmail);

        // Remove the master password and keys to simulate newly registered SSO user
        user.MasterPassword = null;
        user.Key = null;
        user.PrivateKey = null;
        user.PublicKey = null;
        await _userRepository.ReplaceAsync(user);

        // V1 (Obsolete) request format - to be removed with PM-27327
        var request = new
        {
            masterPasswordHash = _newMasterPasswordHash,
            key = _masterKeyWrappedUserKey,
            keys = new
            {
                publicKey = "v1-publicKey",
                encryptedPrivateKey = "v1-encryptedPrivateKey"
            },
            kdf = 0,  // PBKDF2_SHA256
            kdfIterations = 600000,
            kdfMemory = (int?)null,
            kdfParallelism = (int?)null,
            masterPasswordHint = "v1-integration-test-hint",
            orgIdentifier = organization.Identifier
        };

        var jsonRequest = JsonSerializer.Serialize(request, JsonHelpers.CamelCase);

        // Act
        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/set-password");
        message.Content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(message);

        // Assert
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected success but got {response.StatusCode}. Error: {errorContent}");
        }

        // Verify user in database
        var updatedUser = await _userRepository.GetByEmailAsync(userEmail);
        Assert.NotNull(updatedUser);
        Assert.Equal("v1-integration-test-hint", updatedUser.MasterPasswordHint);

        // Verify the master password is hashed and stored
        Assert.NotNull(updatedUser.MasterPassword);
        var verificationResult = _passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.MasterPassword, _newMasterPasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, verificationResult);

        // Verify KDF settings
        Assert.Equal(KdfType.PBKDF2_SHA256, updatedUser.Kdf);
        Assert.Equal(600_000, updatedUser.KdfIterations);
        Assert.Null(updatedUser.KdfMemory);
        Assert.Null(updatedUser.KdfParallelism);

        // Verify timestamps are updated
        Assert.Equal(DateTime.UtcNow, updatedUser.RevisionDate, TimeSpan.FromMinutes(1));
        Assert.Equal(DateTime.UtcNow, updatedUser.AccountRevisionDate, TimeSpan.FromMinutes(1));

        // Verify keys are set (V1 uses Keys property)
        Assert.Equal(_masterKeyWrappedUserKey, updatedUser.Key);
        Assert.Equal("v1-publicKey", updatedUser.PublicKey);
        Assert.Equal("v1-encryptedPrivateKey", updatedUser.PrivateKey);

        // Verify User_ChangedPassword event was logged
        var events = await _eventRepository.GetManyByUserAsync(updatedUser.Id, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1), new PageOptions { PageSize = 100 });
        Assert.NotNull(events);
        Assert.Contains(events.Data, e => e.Type == EventType.User_ChangedPassword && e.UserId == updatedUser.Id);

        // Verify user was accepted into the organization
        var orgUsers = await _organizationUserRepository.GetManyByUserAsync(updatedUser.Id);
        var orgUser = orgUsers.FirstOrDefault(ou => ou.OrganizationId == organization.Id);
        Assert.NotNull(orgUser);
        Assert.Equal(OrganizationUserStatusType.Accepted, orgUser.Status);
    }

    [Theory]
    [BitAutoData]
    public async Task PostSetPasswordAsync_V2_MasterPasswordDecryption_Success(string organizationSsoIdentifier)
    {
        // Arrange - Create organization and user
        var ownerEmail = $"owner-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail);

        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            ownerEmail: ownerEmail,
            name: "Test Org");
        organization.UseSso = true;
        organization.Identifier = organizationSsoIdentifier;
        await _organizationRepository.ReplaceAsync(organization);

        await _ssoConfigRepository.CreateAsync(new SsoConfig
        {
            OrganizationId = organization.Id,
            Enabled = true,
            Data = JsonSerializer.Serialize(new SsoConfigurationData
            {
                MemberDecryptionType = MemberDecryptionType.MasterPassword,
            }, JsonHelpers.CamelCase),
        });

        // Create user with password initially, so we can login
        var userEmail = $"user-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(userEmail);

        // Add user to organization
        var user = await _userRepository.GetByEmailAsync(userEmail);
        Assert.NotNull(user);
        await OrganizationTestHelpers.CreateUserAsync(_factory, organization.Id, userEmail,
            OrganizationUserType.User, userStatusType: OrganizationUserStatusType.Invited);

        // Login as the user
        await _loginHelper.LoginAsync(userEmail);

        // Remove the master password and keys to simulate newly registered SSO user
        user.MasterPassword = null;
        user.Key = null;
        user.PrivateKey = null;
        user.PublicKey = null;
        user.SignedPublicKey = null;
        await _userRepository.ReplaceAsync(user);

        var jsonRequest = CreateV2SetPasswordRequestJson(
            userEmail,
            organization.Identifier,
            "integration-test-hint",
            includeAccountKeys: true);

        // Act
        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/set-password");
        message.Content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(message);

        // Assert
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected success but got {response.StatusCode}. Error: {errorContent}");
        }

        // Verify user in database
        var updatedUser = await _userRepository.GetByEmailAsync(userEmail);
        Assert.NotNull(updatedUser);
        Assert.Equal("integration-test-hint", updatedUser.MasterPasswordHint);

        // Verify the master password is hashed and stored
        Assert.NotNull(updatedUser.MasterPassword);
        var verificationResult = _passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.MasterPassword, _newMasterPasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, verificationResult);

        // Verify KDF settings
        Assert.Equal(KdfType.PBKDF2_SHA256, updatedUser.Kdf);
        Assert.Equal(600_000, updatedUser.KdfIterations);
        Assert.Null(updatedUser.KdfMemory);
        Assert.Null(updatedUser.KdfParallelism);

        // Verify timestamps are updated
        Assert.Equal(DateTime.UtcNow, updatedUser.RevisionDate, TimeSpan.FromMinutes(1));
        Assert.Equal(DateTime.UtcNow, updatedUser.AccountRevisionDate, TimeSpan.FromMinutes(1));

        // Verify keys are set
        Assert.Equal(_masterKeyWrappedUserKey, updatedUser.Key);
        Assert.Equal("publicKey", updatedUser.PublicKey);
        Assert.Equal(_mockEncryptedType7String, updatedUser.PrivateKey);
        Assert.Equal("signedPublicKey", updatedUser.SignedPublicKey);

        // Verify security state
        Assert.Equal(2, updatedUser.SecurityVersion);
        Assert.Equal("v2", updatedUser.SecurityState);

        // Verify signature key pair data
        var signatureKeyPair = await _userSignatureKeyPairRepository.GetByUserIdAsync(updatedUser.Id);
        Assert.NotNull(signatureKeyPair);
        Assert.Equal(Ed25519, signatureKeyPair.SignatureAlgorithm);
        Assert.Equal(_mockEncryptedType7WrappedSigningKey, signatureKeyPair.WrappedSigningKey);
        Assert.Equal("verifyingKey", signatureKeyPair.VerifyingKey);

        // Verify User_ChangedPassword event was logged
        var events = await _eventRepository.GetManyByUserAsync(updatedUser.Id, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1), new PageOptions { PageSize = 100 });
        Assert.NotNull(events);
        Assert.Contains(events.Data, e => e.Type == EventType.User_ChangedPassword && e.UserId == updatedUser.Id);

        // Verify user was accepted into the organization
        var orgUsers = await _organizationUserRepository.GetManyByUserAsync(updatedUser.Id);
        var orgUser = orgUsers.FirstOrDefault(ou => ou.OrganizationId == organization.Id);
        Assert.NotNull(orgUser);
        Assert.Equal(OrganizationUserStatusType.Accepted, orgUser.Status);
    }

    [Theory]
    [BitAutoData]
    public async Task PostSetPasswordAsync_V2_TDEDecryption_Success(string organizationSsoIdentifier)
    {
        // Arrange - Create organization with TDE
        var ownerEmail = $"owner-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail);

        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            ownerEmail: ownerEmail,
            name: "Test Org TDE");
        organization.UseSso = true;
        organization.Identifier = organizationSsoIdentifier;
        await _organizationRepository.ReplaceAsync(organization);

        // Configure SSO for TDE (TrustedDeviceEncryption)
        await _ssoConfigRepository.CreateAsync(new SsoConfig
        {
            OrganizationId = organization.Id,
            Enabled = true,
            Data = JsonSerializer.Serialize(new SsoConfigurationData
            {
                MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption,
            }, JsonHelpers.CamelCase),
        });

        // Create user with password initially, so we can login
        var userEmail = $"user-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(userEmail);

        var user = await _userRepository.GetByEmailAsync(userEmail);
        Assert.NotNull(user);

        // Add user to organization and confirm them (TDE users are confirmed, not invited)
        await OrganizationTestHelpers.CreateUserAsync(_factory, organization.Id, userEmail,
            OrganizationUserType.User, userStatusType: OrganizationUserStatusType.Confirmed);

        // Login as the user
        await _loginHelper.LoginAsync(userEmail);

        // Set up TDE user with V2 account keys but no master password
        // TDE users already have their account keys from device provisioning
        user.MasterPassword = null;
        user.Key = null;
        user.PublicKey = "tde-publicKey";
        user.PrivateKey = _mockEncryptedType7String;
        user.SignedPublicKey = "tde-signedPublicKey";
        user.SecurityVersion = 2;
        user.SecurityState = "v2-tde";
        await _userRepository.ReplaceAsync(user);

        // Create signature key pair for TDE user
        var signatureKeyPairData = new Core.KeyManagement.Models.Data.SignatureKeyPairData(
            Ed25519,
            _mockEncryptedType7WrappedSigningKey,
            "tde-verifyingKey");
        var setSignatureKeyPair = await _userSignatureKeyPairRepository.GetByUserIdAsync(user.Id);
        if (setSignatureKeyPair == null)
        {
            var newKeyPair = new Core.KeyManagement.Entities.UserSignatureKeyPair
            {
                UserId = user.Id,
                SignatureAlgorithm = signatureKeyPairData.SignatureAlgorithm,
                SigningKey = signatureKeyPairData.WrappedSigningKey,
                VerifyingKey = signatureKeyPairData.VerifyingKey,
                CreationDate = DateTime.UtcNow,
                RevisionDate = DateTime.UtcNow
            };
            newKeyPair.SetNewId();
            await _userSignatureKeyPairRepository.CreateAsync(newKeyPair);
        }

        var jsonRequest = CreateV2SetPasswordRequestJson(
            userEmail,
            organization.Identifier,
            "tde-test-hint",
            includeAccountKeys: false);

        // Act
        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/set-password");
        message.Content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(message);

        // Assert
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected success but got {response.StatusCode}. Error: {errorContent}");
        }

        // Verify user in database
        var updatedUser = await _userRepository.GetByEmailAsync(userEmail);
        Assert.NotNull(updatedUser);
        Assert.Equal("tde-test-hint", updatedUser.MasterPasswordHint);

        // Verify the master password is hashed and stored
        Assert.NotNull(updatedUser.MasterPassword);
        var verificationResult = _passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.MasterPassword, _newMasterPasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, verificationResult);

        // Verify KDF settings
        Assert.Equal(KdfType.PBKDF2_SHA256, updatedUser.Kdf);
        Assert.Equal(600_000, updatedUser.KdfIterations);
        Assert.Null(updatedUser.KdfMemory);
        Assert.Null(updatedUser.KdfParallelism);

        // Verify timestamps are updated
        Assert.Equal(DateTime.UtcNow, updatedUser.RevisionDate, TimeSpan.FromMinutes(1));
        Assert.Equal(DateTime.UtcNow, updatedUser.AccountRevisionDate, TimeSpan.FromMinutes(1));

        // Verify key is set
        Assert.Equal(_masterKeyWrappedUserKey, updatedUser.Key);

        // Verify AccountKeys are preserved (TDE users already had V2 keys)
        Assert.Equal("tde-publicKey", updatedUser.PublicKey);
        Assert.Equal(_mockEncryptedType7String, updatedUser.PrivateKey);
        Assert.Equal("tde-signedPublicKey", updatedUser.SignedPublicKey);
        Assert.Equal(2, updatedUser.SecurityVersion);
        Assert.Equal("v2-tde", updatedUser.SecurityState);

        // Verify signature key pair is preserved (TDE users already had signature keys)
        var signatureKeyPair = await _userSignatureKeyPairRepository.GetByUserIdAsync(updatedUser.Id);
        Assert.NotNull(signatureKeyPair);
        Assert.Equal(Ed25519, signatureKeyPair.SignatureAlgorithm);
        Assert.Equal(_mockEncryptedType7WrappedSigningKey, signatureKeyPair.WrappedSigningKey);
        Assert.Equal("tde-verifyingKey", signatureKeyPair.VerifyingKey);

        // Verify User_ChangedPassword event was logged
        var events = await _eventRepository.GetManyByUserAsync(updatedUser.Id, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1), new PageOptions { PageSize = 100 });
        Assert.NotNull(events);
        Assert.Contains(events.Data, e => e.Type == EventType.User_ChangedPassword && e.UserId == updatedUser.Id);

        // Verify user remains confirmed in the organization
        var orgUsers = await _organizationUserRepository.GetManyByUserAsync(updatedUser.Id);
        var orgUser = orgUsers.FirstOrDefault(ou => ou.OrganizationId == organization.Id);
        Assert.NotNull(orgUser);
        Assert.Equal(OrganizationUserStatusType.Confirmed, orgUser.Status);
    }

    [Fact]
    public async Task PostSetPasswordAsync_V2_Unauthorized_ReturnsUnauthorized()
    {
        // Arrange - Don't login
        var jsonRequest = CreateV2SetPasswordRequestJson(
            "test@bitwarden.com",
            "test-org-identifier",
            "test-hint",
            includeAccountKeys: true);

        // Act
        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/set-password");
        message.Content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(message);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostSetPasswordAsync_V2_MismatchedKdfSettings_ReturnsBadRequest()
    {
        // Arrange
        var email = $"kdf-mismatch-test-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        await _loginHelper.LoginAsync(email);

        // Test mismatched KDF settings (600000 vs 650000 iterations)
        var request = new
        {
            masterPasswordAuthentication = new
            {
                kdf = new
                {
                    kdfType = 0,
                    iterations = 600000
                },
                masterPasswordAuthenticationHash = _newMasterPasswordHash,
                salt = email
            },
            masterPasswordUnlock = new
            {
                kdf = new
                {
                    kdfType = 0,
                    iterations = 650000  // Different from authentication KDF
                },
                masterKeyWrappedUserKey = _masterKeyWrappedUserKey,
                salt = email
            },
            accountKeys = new
            {
                userKeyEncryptedAccountPrivateKey = "7.AOs41Hd8OQiCPXjyJKCiDA==",
                accountPublicKey = "public-key"
            },
            orgIdentifier = "test-org-identifier"
        };

        var jsonRequest = JsonSerializer.Serialize(request, JsonHelpers.CamelCase);

        // Act
        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/set-password");
        message.Content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(message);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 1, null, null)]
    [InlineData(KdfType.Argon2id, 4, null, 5)]
    [InlineData(KdfType.Argon2id, 4, 65, null)]
    public async Task PostSetPasswordAsync_V2_InvalidKdfSettings_ReturnsBadRequest(
        KdfType kdf, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        // Arrange
        var email = $"invalid-kdf-test-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        await _loginHelper.LoginAsync(email);

        var jsonRequest = CreateV2SetPasswordRequestJson(
            email,
            "test-org-identifier",
            "test-hint",
            includeAccountKeys: true,
            kdfType: kdf,
            kdfIterations: kdfIterations,
            kdfMemory: kdfMemory,
            kdfParallelism: kdfParallelism);

        // Act
        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/set-password");
        message.Content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json");
        var response = await _client.SendAsync(message);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ===== Email change: legacy path (PM30806_SelfServiceChangeEmailCommand flag OFF) =====
    // These tests pin the flag OFF to exercise the legacy UserService.ChangeEmailAsync path,
    // which rotates the master password and wrapped user key as part of the email change. They
    // share the legacy-shaped PostEmailAsync helper at the end of this block.
    //
    // The class-scoped IFeatureService substitute leaks Returns(...) values across tests in this
    // class, so every test sets the flag explicitly rather than relying on a default.
    //
    // TODO: PM-39120 - On flag cleanup, delete this entire block (all four _SelfServiceFlagOff_
    // tests plus the PostEmailAsync helper). The _SelfServiceFlagOn_ tests further below become
    // the canonical email-change tests; see the note there.
    [Fact]
    public async Task PostEmail_SelfServiceFlagOff_UpdatesEmailAndPassword()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM30806_SelfServiceChangeEmailCommand).Returns(false);
        var newEmail = $"new-email-{Guid.NewGuid()}@bitwarden.com";
        await _loginHelper.LoginAsync(_ownerEmail);

        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);

        var userManager = _factory.GetService<UserManager<User>>();
        var token = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);

        // Act
        var response = await PostEmailAsync(newEmail, token);

        // Assert
        response.EnsureSuccessStatusCode();

        var updatedUser = await _userRepository.GetByEmailAsync(newEmail);
        Assert.NotNull(updatedUser);
        Assert.Equal(newEmail, updatedUser.Email);
        Assert.True(updatedUser.EmailVerified);
        Assert.Equal(_masterKeyWrappedUserKey, updatedUser.Key);
        Assert.Equal(PasswordVerificationResult.Success,
            _passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.MasterPassword!, _newMasterPasswordHash));
        Assert.Equal(newEmail, updatedUser.MasterPasswordSalt);
    }

    [Fact]
    public async Task PostEmail_SelfServiceFlagOff_InvalidMasterPassword_BadRequest()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM30806_SelfServiceChangeEmailCommand).Returns(false);
        var newEmail = $"new-email-{Guid.NewGuid()}@bitwarden.com";
        await _loginHelper.LoginAsync(_ownerEmail);

        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);

        var userManager = _factory.GetService<UserManager<User>>();
        var token = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);

        var requestModel = new EmailRequestModel
        {
            MasterPasswordHash = "wrong_master_password_hash",
            NewEmail = newEmail,
            NewMasterPasswordHash = _newMasterPasswordHash,
            Token = token,
            Key = _masterKeyWrappedUserKey
        };

        // Act
        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/email");
        message.Content = JsonContent.Create(requestModel);
        var response = await _client.SendAsync(message);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify email was not changed
        var unchangedUser = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(unchangedUser);
    }

    [Fact]
    public async Task PostEmail_SelfServiceFlagOff_StripeSyncFails_MasterPasswordSaltIsRolledBack()
    {
        // Arrange
        _featureService.IsEnabled(FeatureFlagKeys.PM30806_SelfServiceChangeEmailCommand).Returns(false);
        var newEmail = $"new-email-{Guid.NewGuid()}@bitwarden.com";
        await _loginHelper.LoginAsync(_ownerEmail);

        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);

        // Set up the user as a Stripe customer to exercise the sync code path in ChangeEmailAsync
        user.Gateway = GatewayType.Stripe;
        user.GatewayCustomerId = "cus_test_stripe_fail";
        await _userRepository.ReplaceAsync(user);

        // Configure the substitute to simulate a Stripe sync failure after the DB write
        _stripeSyncService
            .UpdateCustomerEmailAddressAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException(new Exception("Stripe sync failure")));

        var userManager = _factory.GetService<UserManager<User>>();
        var token = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);

        // Act
        var response = await PostEmailAsync(newEmail, token);

        // Assert - Stripe failure is surfaced as a bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Verify MasterPasswordSalt was rolled back to the original email, not left at newEmail.
        // ChangeEmailAsync sets MasterPasswordSalt = newEmail and persists before attempting Stripe
        // sync; on failure it must re-persist MasterPasswordSalt = previousState.Email.
        var unchangedUser = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(unchangedUser);
        Assert.Equal(_ownerEmail, unchangedUser.MasterPasswordSalt);
    }

    [Fact]
    public async Task PostEmail_SelfServiceFlagOff_MissingNewMasterPasswordHashAndKey_BadRequest()
    {
        // With the flag off, the legacy path still requires NewMasterPasswordHash and Key. The
        // self-service-only payload (no password rotation, no key) must be rejected so legacy
        // clients can't accidentally bypass key rotation.
        _featureService.IsEnabled(FeatureFlagKeys.PM30806_SelfServiceChangeEmailCommand).Returns(false);
        var newEmail = $"new-email-{Guid.NewGuid()}@bitwarden.com";
        await _loginHelper.LoginAsync(_ownerEmail);

        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(user);

        var userManager = _factory.GetService<UserManager<User>>();
        var token = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);

        var response = await PostEmailSelfServiceAsync(newEmail, token, _masterPasswordHash);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var unchangedUser = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(unchangedUser);
        Assert.Equal(_ownerEmail, unchangedUser.Email);
    }

    private async Task<HttpResponseMessage> PostEmailAsync(string newEmail, string token)
    {
        var requestModel = new EmailRequestModel
        {
            MasterPasswordHash = _masterPasswordHash,
            NewEmail = newEmail,
            NewMasterPasswordHash = _newMasterPasswordHash,
            Token = token,
            Key = _masterKeyWrappedUserKey
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/email");
        message.Content = JsonContent.Create(requestModel);
        return await _client.SendAsync(message);
    }

    private static string CreateV2SetPasswordRequestJson(
        string userEmail,
        string orgIdentifier,
        string hint,
        bool includeAccountKeys = true,
        KdfType? kdfType = null,
        int? kdfIterations = null,
        int? kdfMemory = null,
        int? kdfParallelism = null)
    {
        var kdf = new
        {
            kdfType = (int)(kdfType ?? KdfType.PBKDF2_SHA256),
            iterations = kdfIterations ?? 600000,
            memory = kdfMemory,
            parallelism = kdfParallelism
        };

        var request = new
        {
            masterPasswordAuthentication = new
            {
                kdf,
                masterPasswordAuthenticationHash = _newMasterPasswordHash,
                salt = userEmail
            },
            masterPasswordUnlock = new
            {
                kdf,
                masterKeyWrappedUserKey = _masterKeyWrappedUserKey,
                salt = userEmail
            },
            accountKeys = includeAccountKeys ? new
            {
                accountPublicKey = "publicKey",
                userKeyEncryptedAccountPrivateKey = _mockEncryptedType7String,
                publicKeyEncryptionKeyPair = new
                {
                    publicKey = "publicKey",
                    wrappedPrivateKey = _mockEncryptedType7String,
                    signedPublicKey = "signedPublicKey"
                },
                signatureKeyPair = new
                {
                    signatureAlgorithm = "ed25519",
                    wrappedSigningKey = _mockEncryptedType7WrappedSigningKey,
                    verifyingKey = "verifyingKey"
                },
                securityState = new
                {
                    securityVersion = 2,
                    securityState = "v2"
                }
            } : null,
            masterPasswordHint = hint,
            orgIdentifier
        };

        return JsonSerializer.Serialize(request, JsonHelpers.CamelCase);
    }

    // TODO: Delete this test when the PM37165_RotateUserApiKeyCommand flag is cleaned up — the legacy path
    // it covers will be removed along with UserService.RotateApiKeyAsync.
    [Fact]
    public async Task PostRotateApiKey_FlagOff_RotatesApiKey_AndLeavesLastApiKeyRotationDateNull()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM37165_RotateUserApiKeyCommand).Returns(false);
        await _loginHelper.LoginAsync(_ownerEmail);

        var before = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(before);
        var originalApiKey = before.ApiKey;

        var response = await PostRotateApiKeyAsync(_masterPasswordHash);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(after);
        Assert.NotEqual(originalApiKey, after.ApiKey);
        Assert.Equal(30, after.ApiKey.Length);
        Assert.True(after.RevisionDate > before.RevisionDate);
        Assert.Null(after.LastApiKeyRotationDate);
    }

    // TODO: When the PM37165_RotateUserApiKeyCommand flag is cleaned up, rename this to
    // PostRotateApiKey_RotatesApiKey_AndSetsLastApiKeyRotationDate (and drop the flag setup) — it becomes
    // the canonical happy-path integration test.
    [Fact]
    public async Task PostRotateApiKey_FlagOn_RotatesApiKey_AndSetsLastApiKeyRotationDate()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM37165_RotateUserApiKeyCommand).Returns(true);
        await _loginHelper.LoginAsync(_ownerEmail);

        var before = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(before);
        var originalApiKey = before.ApiKey;

        var response = await PostRotateApiKeyAsync(_masterPasswordHash);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(after);
        Assert.NotEqual(originalApiKey, after.ApiKey);
        Assert.Equal(30, after.ApiKey.Length);
        Assert.True(after.RevisionDate > before.RevisionDate);
        Assert.NotNull(after.LastApiKeyRotationDate);
        Assert.True(after.LastApiKeyRotationDate > DateTime.UtcNow.AddMinutes(-1));
    }

    // Bad-secret guard runs before the flag branch, but cover both flag states for parity. When the
    // PM37165_RotateUserApiKeyCommand flag is cleaned up, drop the [InlineData] rows and convert back to [Fact].
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostRotateApiKey_InvalidMasterPasswordHash_BadRequest_AndDoesNotRotate(bool flagOn)
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM37165_RotateUserApiKeyCommand).Returns(flagOn);
        await _loginHelper.LoginAsync(_ownerEmail);

        var before = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(before);

        var response = await PostRotateApiKeyAsync("wrong-master-password-hash");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var after = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(after);
        Assert.Equal(before.ApiKey, after.ApiKey);
        Assert.Null(after.LastApiKeyRotationDate);
    }

    private async Task<HttpResponseMessage> PostRotateApiKeyAsync(string masterPasswordHash)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/rotate-api-key");
        message.Content = JsonContent.Create(new SecretVerificationRequestModel
        {
            MasterPasswordHash = masterPasswordHash
        });
        return await _client.SendAsync(message);
    }

    // ===== Email change: self-service path (PM30806_SelfServiceChangeEmailCommand flag ON) =====
    // These tests pin the flag ON to exercise SelfServiceChangeEmailCommand, which changes the
    // email only — the master password, derived security stamp, and wrapped user key are left
    // untouched. They cover both POST /accounts/email and POST /accounts/email-token and share
    // the PostEmailSelfServiceAsync / PostEmailTokenAsync helpers at the end of this file.
    //
    // TODO: PM-39120 - On flag cleanup, these become the canonical email-change tests: drop the
    // "_SelfServiceFlagOn_" qualifier from each name and remove the per-test
    // _featureService.IsEnabled(...).Returns(true) setup. Delete the _SelfServiceFlagOff_ (legacy)
    // block above wholesale.
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

        var response = await PostEmailSelfServiceAsync(newEmail, token, _masterPasswordHash);

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

        await _pushNotificationService.Received(1).PushSyncSettingsAsync(updatedUser.Id);
        await _pushNotificationService.DidNotReceive()
            .PushLogOutAsync(updatedUser.Id, Arg.Any<bool>(), Arg.Any<PushNotificationLogOutReason?>());
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

        var response = await PostEmailSelfServiceAsync(newEmail, token, "wrong-master-password-hash");

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

        var response = await PostEmailSelfServiceAsync(newEmail, "not-a-valid-token", _masterPasswordHash);

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

        var response = await PostEmailSelfServiceAsync(newEmail, token, _masterPasswordHash);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var unchangedUser = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(unchangedUser);
        Assert.Equal(_ownerEmail, unchangedUser.Email);
    }

    [Fact]
    public async Task PostEmail_SelfServiceFlagOn_KeyConnectorOrgOwner_NotUsingKeyConnector_CanChangeEmail()
    {
        // An owner of a Key Connector organization who doesn't personally use Key Connector must be
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
        await _loginHelper.LoginAsync(_ownerEmail);

        var newEmail = $"new-email-{Guid.NewGuid()}@bitwarden.com";
        var userBefore = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(userBefore);
        // The org uses Key Connector, but the owner themselves does not.
        Assert.False(userBefore.UsesKeyConnector);

        var userManager = _factory.GetService<UserManager<User>>();
        var token = await userManager.GenerateChangeEmailTokenAsync(userBefore, newEmail);

        var response = await PostEmailSelfServiceAsync(newEmail, token, _masterPasswordHash);

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

        var response = await PostEmailSelfServiceAsync(newEmail, token, _masterPasswordHash);

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

        var response = await PostEmailTokenAsync(newEmail, _masterPasswordHash);

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

        var response = await PostEmailTokenAsync(existingEmail, _masterPasswordHash);

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

    // Posts the self-service-shaped payload (no new master password / key) to the merged
    // /accounts/email endpoint. The same endpoint serves both paths; the feature flag picks
    // which command actually runs.
    private async Task<HttpResponseMessage> PostEmailSelfServiceAsync(string newEmail, string token, string masterPasswordHash)
    {
        var requestModel = new EmailRequestModel
        {
            MasterPasswordHash = masterPasswordHash,
            NewEmail = newEmail,
            Token = token
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/accounts/email");
        message.Content = JsonContent.Create(requestModel);
        return await _client.SendAsync(message);
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
