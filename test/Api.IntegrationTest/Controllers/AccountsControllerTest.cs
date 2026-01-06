using System.Net;
using System.Text.Json;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
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
    private readonly IFeatureService _featureService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly IUserSignatureKeyPairRepository _userSignatureKeyPairRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;

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
        _organizationRepository = _factory.GetService<IOrganizationRepository>();
        _ssoConfigRepository = _factory.GetService<ISsoConfigRepository>();
        _userSignatureKeyPairRepository = _factory.GetService<IUserSignatureKeyPairRepository>();
        _eventRepository = _factory.GetService<IEventRepository>();
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
}
