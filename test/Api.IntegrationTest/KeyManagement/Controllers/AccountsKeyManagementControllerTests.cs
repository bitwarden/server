#nullable enable
using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Api.KeyManagement.Models.Responses;
using Bit.Api.Tools.Models.Request;
using Bit.Api.Vault.Models;
using Bit.Api.Vault.Models.Request;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Entities;
using Bit.Core.KeyManagement.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.KeyManagement.Controllers;

public class AccountsKeyManagementControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private static readonly string _mockEncryptedString =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";
    private static readonly string _mockEncryptedType7String = "7.AOs41Hd8OQiCPXjyJKCiDA==";
    private static readonly string _mockEncryptedType7WrappedSigningKey = "7.DRv74Kg1RSlFSam1MNFlGD==";

    private readonly HttpClient _client;
    private readonly IEmergencyAccessRepository _emergencyAccessRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserSignatureKeyPairRepository _userSignatureKeyPairRepository;
    private string _ownerEmail = null!;

    public AccountsKeyManagementControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService.IsEnabled(FeatureFlagKeys.PrivateKeyRegeneration, Arg.Any<bool>())
                .Returns(true);
        });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _deviceRepository = _factory.GetService<IDeviceRepository>();
        _emergencyAccessRepository = _factory.GetService<IEmergencyAccessRepository>();
        _organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        _passwordHasher = _factory.GetService<IPasswordHasher<User>>();
        _organizationRepository = _factory.GetService<IOrganizationRepository>();
        _userSignatureKeyPairRepository = _factory.GetService<IUserSignatureKeyPairRepository>();
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
        localFactory.SubstituteService<IFeatureService>(featureService =>
        {
            featureService.IsEnabled(FeatureFlagKeys.PrivateKeyRegeneration, Arg.Any<bool>())
                .Returns(false);
        });
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

    [Theory]
    [BitAutoData]
    public async Task RotateUserAccountKeysAsync_NotLoggedIn_Unauthorized(
        RotateUserAccountKeysAndDataRequestModel request)
    {
        var response = await _client.PostAsJsonAsync("/accounts/key-management/rotate-user-account-keys", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [BitAutoData]
    public async Task RotateUserAccountKeysAsync_Success(RotateUserAccountKeysAndDataRequestModel request)
    {
        var user = await SetupUserForKeyRotationAsync();
        SetupRotateUserAccountUnlockData(request, user);
        SetupRotateUserAccountData(request);
        SetupRotateUserAccountKeys(request, isV2Crypto: false);

        var response = await _client.PostAsJsonAsync("/accounts/key-management/rotate-user-account-keys", request);
        var responseMessage = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        var userNewState = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(userNewState);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.Email, userNewState.Email);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.KdfType, userNewState.Kdf);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.KdfIterations, userNewState.KdfIterations);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.KdfMemory, userNewState.KdfMemory);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.KdfParallelism, userNewState.KdfParallelism);
    }

    [Theory]
    [BitAutoData]
    public async Task PostSetKeyConnectorKeyAsync_NotLoggedIn_Unauthorized(SetKeyConnectorKeyRequestModel request)
    {
        var response = await _client.PostAsJsonAsync("/accounts/set-key-connector-key", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [BitAutoData]
    public async Task PostSetKeyConnectorKeyAsync_Success(string organizationSsoIdentifier)
    {
        var (ssoUserEmail, organization) = await SetupKeyConnectorTestAsync(OrganizationUserStatusType.Invited, organizationSsoIdentifier);

        var ssoUser = await _userRepository.GetByEmailAsync(ssoUserEmail);
        Assert.NotNull(ssoUser);

        var request = new SetKeyConnectorKeyRequestModel
        {
            Key = _mockEncryptedString,
            Keys = new KeysRequestModel { PublicKey = ssoUser.PublicKey, EncryptedPrivateKey = ssoUser.PrivateKey },
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
            OrgIdentifier = organizationSsoIdentifier
        };

        var response = await _client.PostAsJsonAsync("/accounts/set-key-connector-key", request);
        response.EnsureSuccessStatusCode();

        var user = await _userRepository.GetByEmailAsync(ssoUserEmail);
        Assert.NotNull(user);
        Assert.Equal(request.Key, user.Key);
        Assert.True(user.UsesKeyConnector);
        Assert.Equal(DateTime.UtcNow, user.RevisionDate, TimeSpan.FromMinutes(1));
        Assert.Equal(DateTime.UtcNow, user.AccountRevisionDate, TimeSpan.FromMinutes(1));
        var ssoOrganizationUser = await _organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id);
        Assert.NotNull(ssoOrganizationUser);
        Assert.Equal(OrganizationUserStatusType.Accepted, ssoOrganizationUser.Status);
        Assert.Equal(user.Id, ssoOrganizationUser.UserId);
        Assert.Null(ssoOrganizationUser.Email);
    }

    [Fact]
    public async Task PostSetKeyConnectorKeyAsync_V2_NotLoggedIn_Unauthorized()
    {
        var request = new SetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeyWrappedUserKey = _mockEncryptedString,
            AccountKeys = new AccountKeysRequestModel
            {
                AccountPublicKey = "publicKey",
                UserKeyEncryptedAccountPrivateKey = _mockEncryptedType7String
            },
            OrgIdentifier = "test-org"
        };

        var response = await _client.PostAsJsonAsync("/accounts/set-key-connector-key", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [BitAutoData]
    public async Task PostSetKeyConnectorKeyAsync_V2_Success(string organizationSsoIdentifier)
    {
        var (ssoUserEmail, organization) = await SetupKeyConnectorTestAsync(OrganizationUserStatusType.Invited, organizationSsoIdentifier);

        var request = new SetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeyWrappedUserKey = _mockEncryptedString,
            AccountKeys = new AccountKeysRequestModel
            {
                AccountPublicKey = "publicKey",
                UserKeyEncryptedAccountPrivateKey = _mockEncryptedType7String,
                PublicKeyEncryptionKeyPair = new PublicKeyEncryptionKeyPairRequestModel
                {
                    PublicKey = "publicKey",
                    WrappedPrivateKey = _mockEncryptedType7String,
                    SignedPublicKey = "signedPublicKey"
                },
                SignatureKeyPair = new SignatureKeyPairRequestModel
                {
                    SignatureAlgorithm = "ed25519",
                    WrappedSigningKey = _mockEncryptedType7WrappedSigningKey,
                    VerifyingKey = "verifyingKey"
                },
                SecurityState = new SecurityStateModel
                {
                    SecurityVersion = 2,
                    SecurityState = "v2"
                }
            },
            OrgIdentifier = organizationSsoIdentifier
        };

        var response = await _client.PostAsJsonAsync("/accounts/set-key-connector-key", request);
        response.EnsureSuccessStatusCode();

        var user = await _userRepository.GetByEmailAsync(ssoUserEmail);
        Assert.NotNull(user);
        Assert.Equal(request.KeyConnectorKeyWrappedUserKey, user.Key);
        Assert.True(user.UsesKeyConnector);
        Assert.Equal(KdfType.Argon2id, user.Kdf);
        Assert.Equal(AuthConstants.ARGON2_ITERATIONS.Default, user.KdfIterations);
        Assert.Equal(AuthConstants.ARGON2_MEMORY.Default, user.KdfMemory);
        Assert.Equal(AuthConstants.ARGON2_PARALLELISM.Default, user.KdfParallelism);
        Assert.Equal(request.AccountKeys.PublicKeyEncryptionKeyPair!.SignedPublicKey, user.SignedPublicKey);
        Assert.Equal(request.AccountKeys.SecurityState!.SecurityState, user.SecurityState);
        Assert.Equal(request.AccountKeys.SecurityState.SecurityVersion, user.SecurityVersion);
        Assert.Equal(DateTime.UtcNow, user.RevisionDate, TimeSpan.FromMinutes(1));
        Assert.Equal(DateTime.UtcNow, user.AccountRevisionDate, TimeSpan.FromMinutes(1));

        var ssoOrganizationUser =
            await _organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id);
        Assert.NotNull(ssoOrganizationUser);
        Assert.Equal(OrganizationUserStatusType.Accepted, ssoOrganizationUser.Status);
        Assert.Equal(user.Id, ssoOrganizationUser.UserId);
        Assert.Null(ssoOrganizationUser.Email);

        var signatureKeyPair = await _userSignatureKeyPairRepository.GetByUserIdAsync(user.Id);
        Assert.NotNull(signatureKeyPair);
        Assert.Equal(SignatureAlgorithm.Ed25519, signatureKeyPair.SignatureAlgorithm);
        Assert.Equal(_mockEncryptedType7WrappedSigningKey, signatureKeyPair.WrappedSigningKey);
        Assert.Equal("verifyingKey", signatureKeyPair.VerifyingKey);
    }

    [Fact]
    public async Task PostConvertToKeyConnectorAsync_NotLoggedIn_Unauthorized()
    {
        var response = await _client.PostAsJsonAsync("/accounts/convert-to-key-connector", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostConvertToKeyConnectorAsync_Success()
    {
        var (ssoUserEmail, organization) = await SetupKeyConnectorTestAsync(OrganizationUserStatusType.Accepted);

        var response = await _client.PostAsJsonAsync("/accounts/convert-to-key-connector", new { });
        response.EnsureSuccessStatusCode();

        var user = await _userRepository.GetByEmailAsync(ssoUserEmail);
        Assert.NotNull(user);
        Assert.Null(user.MasterPassword);
        Assert.True(user.UsesKeyConnector);
        Assert.Equal(DateTime.UtcNow, user.RevisionDate, TimeSpan.FromMinutes(1));
        Assert.Equal(DateTime.UtcNow, user.AccountRevisionDate, TimeSpan.FromMinutes(1));
    }

    [Theory]
    [BitAutoData]
    public async Task RotateV2UserAccountKeysAsync_Success(RotateUserAccountKeysAndDataRequestModel request)
    {
        var user = await SetupUserForKeyRotationAsync(_mockEncryptedType7String, createSignatureKeyPair: true);
        SetupRotateUserAccountUnlockData(request, user);
        SetupRotateUserAccountData(request);
        SetupRotateUserAccountKeys(request, isV2Crypto: true);

        var response = await _client.PostAsJsonAsync("/accounts/key-management/rotate-user-account-keys", request);
        var responseMessage = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        var userNewState = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(userNewState);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.Email, userNewState.Email);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.KdfType, userNewState.Kdf);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.KdfIterations, userNewState.KdfIterations);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.KdfMemory, userNewState.KdfMemory);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.KdfParallelism, userNewState.KdfParallelism);

        // Assert V2-specific fields
        Assert.Equal(request.AccountKeys.PublicKeyEncryptionKeyPair!.SignedPublicKey, userNewState.SignedPublicKey);
        Assert.Equal(request.AccountKeys.SecurityState!.SecurityState, userNewState.SecurityState);
        Assert.Equal(request.AccountKeys.SecurityState.SecurityVersion, userNewState.SecurityVersion);

        var signatureKeyPair = await _userSignatureKeyPairRepository.GetByUserIdAsync(userNewState.Id);
        Assert.NotNull(signatureKeyPair);
        Assert.Equal(SignatureAlgorithm.Ed25519, signatureKeyPair.SignatureAlgorithm);
        Assert.Equal(request.AccountKeys.SignatureKeyPair!.WrappedSigningKey, signatureKeyPair.WrappedSigningKey);
        Assert.Equal(request.AccountKeys.SignatureKeyPair.VerifyingKey, signatureKeyPair.VerifyingKey);
    }

    [Theory]
    [BitAutoData]
    public async Task RotateUpgradeToV2UserAccountKeysAsync_Success(RotateUserAccountKeysAndDataRequestModel request)
    {
        var user = await SetupUserForKeyRotationAsync();
        SetupRotateUserAccountUnlockData(request, user);
        SetupRotateUserAccountData(request);
        SetupRotateUserAccountKeys(request, isV2Crypto: true);

        var response = await _client.PostAsJsonAsync("/accounts/key-management/rotate-user-account-keys", request);
        var responseMessage = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        var userNewState = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(userNewState);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.Email, userNewState.Email);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.KdfType, userNewState.Kdf);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.KdfIterations, userNewState.KdfIterations);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.KdfMemory, userNewState.KdfMemory);
        Assert.Equal(request.AccountUnlockData.MasterPasswordUnlockData.KdfParallelism, userNewState.KdfParallelism);

        // Assert V2 upgrade-specific fields
        Assert.Equal(request.AccountKeys.PublicKeyEncryptionKeyPair!.SignedPublicKey, userNewState.SignedPublicKey);
        Assert.Equal(request.AccountKeys.SecurityState!.SecurityState, userNewState.SecurityState);
        Assert.Equal(request.AccountKeys.SecurityState.SecurityVersion, userNewState.SecurityVersion);

        var signatureKeyPair = await _userSignatureKeyPairRepository.GetByUserIdAsync(userNewState.Id);
        Assert.NotNull(signatureKeyPair);
        Assert.Equal(SignatureAlgorithm.Ed25519, signatureKeyPair.SignatureAlgorithm);
        Assert.Equal(request.AccountKeys.SignatureKeyPair!.WrappedSigningKey, signatureKeyPair.WrappedSigningKey);
        Assert.Equal(request.AccountKeys.SignatureKeyPair.VerifyingKey, signatureKeyPair.VerifyingKey);
    }

    [Theory]
    [BitAutoData]
    public async Task RotateUserAccountKeys_V1Crypto_WithV2UpgradeToken_PersistsTokenToDatabase(
        RotateUserAccountKeysAndDataRequestModel request)
    {
        var user = await SetupUserForKeyRotationAsync();
        SetupRotateUserAccountUnlockData(request, user);
        SetupRotateUserAccountData(request);
        SetupRotateUserAccountKeys(request, isV2Crypto: false);
        request.AccountUnlockData.V2UpgradeToken = new V2UpgradeTokenRequestModel
        {
            WrappedUserKey1 = _mockEncryptedString,
            WrappedUserKey2 = _mockEncryptedString
        };

        var response = await _client.PostAsJsonAsync("/accounts/key-management/rotate-user-account-keys", request);
        response.EnsureSuccessStatusCode();

        var userNewState = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(userNewState);
        Assert.NotNull(userNewState.V2UpgradeToken);
        Assert.Contains($"\"WrappedUserKey1\":\"{_mockEncryptedString}\"", userNewState.V2UpgradeToken);
        Assert.Contains($"\"WrappedUserKey2\":\"{_mockEncryptedString}\"", userNewState.V2UpgradeToken);
    }

    [Theory]
    [BitAutoData]
    public async Task RotateUserAccountKeys_V2Crypto_WithV2UpgradeToken_PersistsTokenToDatabase(
        RotateUserAccountKeysAndDataRequestModel request)
    {
        var user = await SetupUserForKeyRotationAsync(_mockEncryptedType7String, createSignatureKeyPair: true);
        SetupRotateUserAccountUnlockData(request, user);
        SetupRotateUserAccountData(request);
        SetupRotateUserAccountKeys(request, isV2Crypto: true);
        request.AccountUnlockData.V2UpgradeToken = new V2UpgradeTokenRequestModel
        {
            WrappedUserKey1 = _mockEncryptedString,
            WrappedUserKey2 = _mockEncryptedString
        };

        var response = await _client.PostAsJsonAsync("/accounts/key-management/rotate-user-account-keys", request);
        response.EnsureSuccessStatusCode();

        var userNewState = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(userNewState);
        Assert.NotNull(userNewState.V2UpgradeToken);
        Assert.Contains($"\"WrappedUserKey1\":\"{_mockEncryptedString}\"", userNewState.V2UpgradeToken);
        Assert.Contains($"\"WrappedUserKey2\":\"{_mockEncryptedString}\"", userNewState.V2UpgradeToken);
    }

    [Theory]
    [BitAutoData]
    public async Task RotateUserAccountKeys_WithoutV2UpgradeToken_DoesNotSetToken(
        RotateUserAccountKeysAndDataRequestModel request)
    {
        var user = await SetupUserForKeyRotationAsync();
        SetupRotateUserAccountUnlockData(request, user);
        SetupRotateUserAccountData(request);
        SetupRotateUserAccountKeys(request, isV2Crypto: false);

        var response = await _client.PostAsJsonAsync("/accounts/key-management/rotate-user-account-keys", request);
        response.EnsureSuccessStatusCode();

        var userNewState = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(userNewState);
        Assert.Null(userNewState.V2UpgradeToken);
    }

    [Theory]
    [BitAutoData]
    public async Task RotateUserAccountKeys_WithExistingV2UpgradeToken_WithoutNewToken_ClearsStaleToken(
        RotateUserAccountKeysAndDataRequestModel request)
    {
        // Arrange
        var user = await SetupUserForKeyRotationAsync();

        // Add existing stale token to user BEFORE rotation
        var staleToken = new V2UpgradeTokenData
        {
            WrappedUserKey1 = _mockEncryptedString,
            WrappedUserKey2 = _mockEncryptedString
        };
        user.V2UpgradeToken = staleToken.ToJson();
        await _userRepository.ReplaceAsync(user);

        // Setup request WITHOUT V2UpgradeToken
        SetupRotateUserAccountUnlockData(request, user);
        SetupRotateUserAccountData(request);
        SetupRotateUserAccountKeys(request, isV2Crypto: false);
        request.AccountUnlockData.V2UpgradeToken = null; // Explicit: No new token

        // Act
        var response = await _client.PostAsJsonAsync("/accounts/key-management/rotate-user-account-keys", request);
        response.EnsureSuccessStatusCode();

        // Assert
        var userNewState = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(userNewState);

        // Critical: Verify stale token is cleared
        Assert.Null(userNewState.V2UpgradeToken);

        // Verify logout behavior (SecurityStamp should be different)
        Assert.NotEqual(user.SecurityStamp, userNewState.SecurityStamp);
    }

    [Theory]
    [BitAutoData]
    public async Task RotateUserAccountKeys_WithExistingV2UpgradeToken_WithNewToken_ReplacesToken(
        RotateUserAccountKeysAndDataRequestModel request)
    {
        // Arrange
        var user = await SetupUserForKeyRotationAsync();

        // Add existing old token to user BEFORE rotation
        // Use Type 2 encryption strings for the old token
        var oldToken = new V2UpgradeTokenData
        {
            WrappedUserKey1 = "2.OLD1==|OLD1data==|OLD1hmac==",
            WrappedUserKey2 = "2.OLD2==|OLD2data==|OLD2hmac=="
        };
        user.V2UpgradeToken = oldToken.ToJson();
        await _userRepository.ReplaceAsync(user);

        // Setup request WITH new V2UpgradeToken
        SetupRotateUserAccountUnlockData(request, user);
        SetupRotateUserAccountData(request);
        SetupRotateUserAccountKeys(request, isV2Crypto: false);
        request.AccountUnlockData.V2UpgradeToken = new V2UpgradeTokenRequestModel
        {
            WrappedUserKey1 = _mockEncryptedString,
            WrappedUserKey2 = _mockEncryptedString
        };

        // Act
        var response = await _client.PostAsJsonAsync("/accounts/key-management/rotate-user-account-keys", request);
        response.EnsureSuccessStatusCode();

        // Assert
        var userNewState = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(userNewState);
        Assert.NotNull(userNewState.V2UpgradeToken);

        // Verify new token is present
        Assert.Contains($"\"WrappedUserKey1\":\"{_mockEncryptedString}\"", userNewState.V2UpgradeToken);
        Assert.Contains($"\"WrappedUserKey2\":\"{_mockEncryptedString}\"", userNewState.V2UpgradeToken);

        // Verify old token is NOT present
        Assert.DoesNotContain("OLD1", userNewState.V2UpgradeToken);
        Assert.DoesNotContain("OLD2", userNewState.V2UpgradeToken);

        // Verify NO logout (SecurityStamp should be the same for key rotation with token)
        Assert.Equal(user.SecurityStamp, userNewState.SecurityStamp);
    }

    [Theory]
    [BitAutoData]
    public async Task RotateUserAccountKeys_V2Crypto_WithExistingV2UpgradeToken_WithoutNewToken_ClearsStaleToken(
        RotateUserAccountKeysAndDataRequestModel request)
    {
        // Arrange
        var user = await SetupUserForKeyRotationAsync(_mockEncryptedType7String, createSignatureKeyPair: true);

        // Add existing stale token to V2 crypto user BEFORE rotation
        var staleToken = new V2UpgradeTokenData
        {
            WrappedUserKey1 = _mockEncryptedString,
            WrappedUserKey2 = _mockEncryptedString
        };
        user.V2UpgradeToken = staleToken.ToJson();
        await _userRepository.ReplaceAsync(user);

        // Setup request WITHOUT V2UpgradeToken
        SetupRotateUserAccountUnlockData(request, user);
        SetupRotateUserAccountData(request);
        SetupRotateUserAccountKeys(request, isV2Crypto: true);
        request.AccountUnlockData.V2UpgradeToken = null; // Explicit: No new token

        // Act
        var response = await _client.PostAsJsonAsync("/accounts/key-management/rotate-user-account-keys", request);
        response.EnsureSuccessStatusCode();

        // Assert
        var userNewState = await _userRepository.GetByEmailAsync(_ownerEmail);
        Assert.NotNull(userNewState);

        // Critical: Verify stale token is cleared for V2 crypto users
        Assert.Null(userNewState.V2UpgradeToken);

        // Verify logout behavior (SecurityStamp should be different)
        Assert.NotEqual(user.SecurityStamp, userNewState.SecurityStamp);
    }

    [Fact]
    public async Task GetKeyConnectorConfirmationDetailsAsync_Success()
    {
        var (ssoUserEmail, organization) = await SetupKeyConnectorTestAsync(OrganizationUserStatusType.Invited);

        await OrganizationTestHelpers.CreateUserAsync(_factory, organization.Id, ssoUserEmail,
            OrganizationUserType.User, userStatusType: OrganizationUserStatusType.Accepted);

        var response = await _client.GetAsync($"/accounts/key-connector/confirmation-details/{organization.Identifier}");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<KeyConnectorConfirmationDetailsResponseModel>();

        Assert.NotNull(result);
        Assert.Equal(organization.Name, result.OrganizationName);
    }

    private async Task<(string, Organization)> SetupKeyConnectorTestAsync(OrganizationUserStatusType userStatusType,
        string organizationSsoIdentifier = "test-sso-identifier")
    {
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            PlanType.EnterpriseAnnually, _ownerEmail, passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);
        organization.UseKeyConnector = true;
        organization.UseSso = true;
        organization.Identifier = organizationSsoIdentifier;
        await _organizationRepository.ReplaceAsync(organization);

        var ssoUserEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ssoUserEmail);
        await _loginHelper.LoginAsync(ssoUserEmail);

        await OrganizationTestHelpers.CreateUserAsync(_factory, organization.Id, ssoUserEmail,
            OrganizationUserType.User, userStatusType: userStatusType);

        return (ssoUserEmail, organization);
    }

    private async Task<User> SetupUserForKeyRotationAsync(
        string? privateKey = null,
        bool createSignatureKeyPair = false)
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var user = await _userRepository.GetByEmailAsync(_ownerEmail);
        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var password = _passwordHasher.HashPassword(user, "newMasterPassword");
        user.MasterPassword = password;
        user.PublicKey = "publicKey";
        user.PrivateKey = privateKey ?? _mockEncryptedString;

        // If creating signature key pair, user should already have V2 signed state
        if (createSignatureKeyPair)
        {
            user.SignedPublicKey = "signedPublicKey";
            user.SecurityState = "v2";
            user.SecurityVersion = 2;
        }

        await _userRepository.ReplaceAsync(user);

        if (createSignatureKeyPair)
        {
            await _userSignatureKeyPairRepository.CreateAsync(new UserSignatureKeyPair
            {
                UserId = user.Id,
                SignatureAlgorithm = SignatureAlgorithm.Ed25519,
                SigningKey = _mockEncryptedType7String,
                VerifyingKey = "verifyingKey",
            });
        }

        return user;
    }

    private void SetupRotateUserAccountUnlockData(
        RotateUserAccountKeysAndDataRequestModel request,
        User user)
    {
        // KDF settings
        request.AccountUnlockData.MasterPasswordUnlockData.KdfType = user.Kdf;
        request.AccountUnlockData.MasterPasswordUnlockData.KdfIterations = user.KdfIterations;
        request.AccountUnlockData.MasterPasswordUnlockData.KdfMemory = user.KdfMemory;
        request.AccountUnlockData.MasterPasswordUnlockData.KdfParallelism = user.KdfParallelism;
        request.AccountUnlockData.MasterPasswordUnlockData.Email = user.Email;
        request.AccountUnlockData.MasterPasswordUnlockData.MasterKeyEncryptedUserKey = _mockEncryptedString;

        // Unlock data arrays
        request.AccountUnlockData.PasskeyUnlockData = [];
        request.AccountUnlockData.DeviceKeyUnlockData = [];
        request.AccountUnlockData.EmergencyAccessUnlockData = [];
        request.AccountUnlockData.OrganizationAccountRecoveryUnlockData = [];

        // Authentication hash
        request.OldMasterKeyAuthenticationHash = "newMasterPassword";
    }

    private void SetupRotateUserAccountData(RotateUserAccountKeysAndDataRequestModel request)
    {
        request.AccountData.Ciphers =
        [
            new CipherWithIdRequestModel
            {
                Id = Guid.NewGuid(),
                Type = CipherType.Login,
                Name = _mockEncryptedString,
                Login = new CipherLoginModel
                {
                    Username = _mockEncryptedString,
                    Password = _mockEncryptedString,
                },
            },
        ];

        request.AccountData.Folders =
        [
            new FolderWithIdRequestModel
            {
                Id = Guid.NewGuid(),
                Name = _mockEncryptedString,
            },
        ];

        request.AccountData.Sends =
        [
            new SendWithIdRequestModel
            {
                Id = Guid.NewGuid(),
                Name = _mockEncryptedString,
                Key = _mockEncryptedString,
                Disabled = false,
                DeletionDate = DateTime.UtcNow.AddDays(1),
            },
        ];
    }

    private void SetupRotateUserAccountKeys(
        RotateUserAccountKeysAndDataRequestModel request,
        bool isV2Crypto)
    {
        request.AccountKeys.AccountPublicKey = "publicKey";

        if (isV2Crypto)
        {
            // V2 crypto: Type 7 encryption with V2 keys and SecurityState
            request.AccountKeys.UserKeyEncryptedAccountPrivateKey = _mockEncryptedType7String;
            request.AccountKeys.PublicKeyEncryptionKeyPair = new PublicKeyEncryptionKeyPairRequestModel
            {
                PublicKey = "publicKey",
                WrappedPrivateKey = _mockEncryptedType7String,
                SignedPublicKey = "signedPublicKey",
            };
            request.AccountKeys.SignatureKeyPair = new SignatureKeyPairRequestModel
            {
                SignatureAlgorithm = "ed25519",
                WrappedSigningKey = _mockEncryptedType7String,
                VerifyingKey = "verifyingKey",
            };
            request.AccountKeys.SecurityState = new SecurityStateModel
            {
                SecurityVersion = 2,
                SecurityState = "v2",
            };
        }
        else
        {
            // V1 crypto: Type 2 encryption, no V2 keys
            request.AccountKeys.UserKeyEncryptedAccountPrivateKey = _mockEncryptedString;
            request.AccountKeys.PublicKeyEncryptionKeyPair = null;
            request.AccountKeys.SignatureKeyPair = null;
            request.AccountKeys.SecurityState = null;
        }

        request.AccountUnlockData.V2UpgradeToken = null;
    }
}
