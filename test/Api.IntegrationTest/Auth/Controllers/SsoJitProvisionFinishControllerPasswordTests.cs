using System.Net;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;
using IFeatureService = Bitwarden.Server.Sdk.Features.IFeatureService;

namespace Bit.Api.IntegrationTest.Auth.Controllers;

public class SsoJitProvisionFinishControllerPasswordTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _endpoint = "/sso-jit-provision-finish/password";
    private const string _masterKeyWrappedUserKey =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";
    private const string _mockEncryptedType7String = "7.AOs41Hd8OQiCPXjyJKCiDA==";
    private const string _mockEncryptedType7WrappedSigningKey = "7.DRv74Kg1RSlFSam1MNFlGD==";
    private const string _newMasterPasswordHash = "new_master_password_hash";
    private const string _hint = "jit-finish-hint";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IFeatureService _featureService;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly IPasswordHasher<User> _passwordHasher;

    public SsoJitProvisionFinishControllerPasswordTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _factory.SubstituteService<IFeatureService>(_ => { });
        _factory.SubstituteService<IStripeSyncService>(_ => { });
        _factory.SubstituteService<IMailService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _featureService = _factory.GetService<IFeatureService>();
        _userRepository = _factory.GetService<IUserRepository>();
        _organizationRepository = _factory.GetService<IOrganizationRepository>();
        _organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        _ssoConfigRepository = _factory.GetService<ISsoConfigRepository>();
        _passwordHasher = _factory.GetService<IPasswordHasher<User>>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(false, OrganizationUserStatusType.Invited)]
    [InlineData(true, OrganizationUserStatusType.Accepted)]
    public async Task FinishJitProvisionForPassword_V2_SetsPasswordAndKeys_AppliesAcceptOrganizationInvite(
        bool acceptOrganizationInvite, OrganizationUserStatusType expectedStatus)
    {
        // Arrange
        SetV2MpJitFlag(true);
        var (organization, userEmail) = await ArrangeInvitedSsoJitMasterPasswordUserAsync();
        var json = CreateModernRequestJson(userEmail, organization.Identifier!, acceptOrganizationInvite,
            includeAccountKeys: true);

        // Act
        var response = await PostAsync(json);

        // Assert
        await AssertSuccessAsync(response);
        var updatedUser = await AssertMasterPasswordSetAsync(userEmail);
        Assert.Equal("publicKey", updatedUser.PublicKey);
        Assert.Equal(_mockEncryptedType7String, updatedUser.PrivateKey);
        Assert.Equal("signedPublicKey", updatedUser.SignedPublicKey);
        Assert.Equal(2, updatedUser.SecurityVersion);
        await AssertOrgUserStatusAsync(organization.Id, updatedUser.Id, expectedStatus);
    }

    [Theory]
    [InlineData(false, OrganizationUserStatusType.Invited)]
    [InlineData(true, OrganizationUserStatusType.Accepted)]
    public async Task FinishJitProvisionForPassword_V1_SetsPasswordAndKeys_AppliesAcceptOrganizationInvite(
        bool acceptOrganizationInvite, OrganizationUserStatusType expectedStatus)
    {
        // Arrange - legacy Keys routes to the V1 command.
        SetV2MpJitFlag(false);
        var (organization, userEmail) = await ArrangeInvitedSsoJitMasterPasswordUserAsync();
        var json = CreateModernRequestJson(userEmail, organization.Identifier!, acceptOrganizationInvite,
            includeAccountKeys: false, includeLegacyKeys: true);

        // Act
        var response = await PostAsync(json);

        // Assert
        await AssertSuccessAsync(response);
        var updatedUser = await AssertMasterPasswordSetAsync(userEmail);
        Assert.Equal("v1-publicKey", updatedUser.PublicKey);
        Assert.Equal("v1-encryptedPrivateKey", updatedUser.PrivateKey);
        await AssertOrgUserStatusAsync(organization.Id, updatedUser.Id, expectedStatus);
    }

    [Fact]
    public async Task FinishJitProvisionForPassword_AcceptOrganizationInviteOmitted_ReturnsBadRequest()
    {
        // Arrange
        SetV2MpJitFlag(true);
        var (organization, userEmail) = await ArrangeInvitedSsoJitMasterPasswordUserAsync();
        var json = CreateModernRequestJson(userEmail, organization.Identifier!, acceptOrganizationInvite: null,
            includeAccountKeys: true);

        // Act
        var response = await PostAsync(json);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertUserUnchangedAsync(organization.Id, userEmail);
    }

    [Fact]
    public async Task FinishJitProvisionForPassword_V2FlagOff_WithAccountKeys_ReturnsBadRequest()
    {
        // Arrange
        SetV2MpJitFlag(false);
        var (organization, userEmail) = await ArrangeInvitedSsoJitMasterPasswordUserAsync();
        var json = CreateModernRequestJson(userEmail, organization.Identifier!, acceptOrganizationInvite: false,
            includeAccountKeys: true);

        // Act
        var response = await PostAsync(json);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var user = await _userRepository.GetByEmailAsync(userEmail);
        Assert.NotNull(user);
        Assert.Null(user.MasterPassword);
        Assert.Null(user.Key);
        await AssertOrgUserStatusAsync(organization.Id, user.Id, OrganizationUserStatusType.Invited);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinishJitProvisionForPassword_WithoutKeypair_ReturnsBadRequest(bool v2FlagEnabled)
    {
        // Arrange
        SetV2MpJitFlag(v2FlagEnabled);
        var (organization, userEmail) = await ArrangeInvitedSsoJitMasterPasswordUserAsync();
        var json = CreateModernRequestJson(userEmail, organization.Identifier!, acceptOrganizationInvite: true,
            includeAccountKeys: false);

        // Act
        var response = await PostAsync(json);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertUserUnchangedAsync(organization.Id, userEmail);
    }

    [Theory]
    [InlineData("masterPasswordAuthentication", false)]
    [InlineData("masterPasswordAuthentication", true)]
    [InlineData("masterPasswordUnlock", false)]
    [InlineData("masterPasswordUnlock", true)]
    public async Task FinishJitProvisionForPassword_MasterPasswordDataMissing_ReturnsBadRequest(
        string propertyName, bool sendExplicitNull)
    {
        // Arrange
        SetV2MpJitFlag(true);
        var (organization, userEmail) = await ArrangeInvitedSsoJitMasterPasswordUserAsync();
        var request = CreateModernRequest(userEmail, organization.Identifier!, acceptOrganizationInvite: true,
            includeAccountKeys: true);
        if (sendExplicitNull)
        {
            request[propertyName] = null;
        }
        else
        {
            request.Remove(propertyName);
        }

        // Act
        var response = await PostAsync(JsonSerializer.Serialize(request));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertUserUnchangedAsync(organization.Id, userEmail);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinishJitProvisionForPassword_AccountKeysAlreadyInitialized_ReturnsBadRequest(
        bool v2FlagEnabled)
    {
        // Arrange
        SetV2MpJitFlag(v2FlagEnabled);
        var (organization, userEmail) = await ArrangeInvitedSsoJitMasterPasswordUserAsync();
        var user = await _userRepository.GetByEmailAsync(userEmail);
        Assert.NotNull(user);
        user.PublicKey = "existingPublicKey";
        user.PrivateKey = "existingPrivateKey";
        await _userRepository.ReplaceAsync(user);

        var json = CreateModernRequestJson(userEmail, organization.Identifier!, acceptOrganizationInvite: true,
            includeAccountKeys: v2FlagEnabled, includeLegacyKeys: !v2FlagEnabled);

        // Act
        var response = await PostAsync(json);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var updatedUser = await _userRepository.GetByEmailAsync(userEmail);
        Assert.NotNull(updatedUser);
        Assert.Null(updatedUser.MasterPassword);
        Assert.Null(updatedUser.Key);
        Assert.Equal("existingPublicKey", updatedUser.PublicKey);
        Assert.Equal("existingPrivateKey", updatedUser.PrivateKey);
        await AssertOrgUserStatusAsync(organization.Id, updatedUser.Id, OrganizationUserStatusType.Invited);
    }

    [Fact]
    public async Task FinishJitProvisionForPassword_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange - no login
        var json = CreateModernRequestJson("test@bitwarden.com", "test-org-identifier", acceptOrganizationInvite: false,
            includeAccountKeys: true);

        // Act
        var response = await PostAsync(json);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private void SetV2MpJitFlag(bool enabled)
    {
        _featureService.IsEnabled(FeatureFlagKeys.EnableAccountEncryptionV2JitPasswordRegistration).Returns(enabled);
    }

    /// <summary>
    /// Creates an SSO organization with master password decryption and an Invited member whose account has no
    /// master password or keys, then authenticates the client as that member.
    /// </summary>
    private async Task<(Organization organization, string userEmail)> ArrangeInvitedSsoJitMasterPasswordUserAsync()
    {
        var ownerEmail = $"owner-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail);

        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            ownerEmail: ownerEmail,
            name: "SSO JIT Finish Org");
        organization.UseSso = true;
        organization.Identifier = $"sso-{Guid.NewGuid()}";
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

        var userEmail = $"user-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(userEmail);
        var user = await _userRepository.GetByEmailAsync(userEmail);
        Assert.NotNull(user);

        await OrganizationTestHelpers.CreateUserAsync(_factory, organization.Id, userEmail,
            OrganizationUserType.User, userStatusType: OrganizationUserStatusType.Invited);

        await _loginHelper.LoginAsync(userEmail);

        // Simulate a newly JIT provisioned SSO user.
        user.MasterPassword = null;
        user.Key = null;
        user.PrivateKey = null;
        user.PublicKey = null;
        user.SignedPublicKey = null;
        await _userRepository.ReplaceAsync(user);

        return (organization, userEmail);
    }

    private async Task<HttpResponseMessage> PostAsync(string json)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        message.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return await _client.SendAsync(message);
    }

    private static async Task AssertSuccessAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected success but got {response.StatusCode}. Error: {errorContent}");
        }
    }

    private async Task<User> AssertMasterPasswordSetAsync(string userEmail)
    {
        var updatedUser = await _userRepository.GetByEmailAsync(userEmail);
        Assert.NotNull(updatedUser);
        Assert.NotNull(updatedUser.MasterPassword);
        Assert.Equal(PasswordVerificationResult.Success,
            _passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.MasterPassword, _newMasterPasswordHash));
        Assert.Equal(_masterKeyWrappedUserKey, updatedUser.Key);
        Assert.Equal(_hint, updatedUser.MasterPasswordHint);
        Assert.Equal(KdfType.PBKDF2_SHA256, updatedUser.Kdf);
        Assert.Equal(600_000, updatedUser.KdfIterations);
        Assert.Equal(userEmail, updatedUser.MasterPasswordSalt);
        return updatedUser;
    }

    private async Task AssertUserUnchangedAsync(Guid organizationId, string userEmail)
    {
        var user = await _userRepository.GetByEmailAsync(userEmail);
        Assert.NotNull(user);
        Assert.Null(user.MasterPassword);
        Assert.Null(user.Key);
        Assert.Null(user.PublicKey);
        Assert.Null(user.PrivateKey);
        await AssertOrgUserStatusAsync(organizationId, user.Id, OrganizationUserStatusType.Invited);
    }

    private async Task AssertOrgUserStatusAsync(Guid organizationId, Guid userId,
        OrganizationUserStatusType expectedStatus)
    {
        var orgUser = await _organizationUserRepository.GetByOrganizationAsync(organizationId, userId);
        Assert.NotNull(orgUser);
        Assert.Equal(expectedStatus, orgUser.Status);
    }

    private static void AddAcceptOrganizationInvite(Dictionary<string, object?> request, bool? acceptOrganizationInvite)
    {
        // Omit the property entirely when null to exercise the required-property check.
        if (acceptOrganizationInvite.HasValue)
        {
            request["acceptOrganizationInvite"] = acceptOrganizationInvite.Value;
        }
    }

    private static string CreateModernRequestJson(string userEmail, string orgIdentifier,
        bool? acceptOrganizationInvite, bool includeAccountKeys, bool includeLegacyKeys = false)
    {
        return JsonSerializer.Serialize(CreateModernRequest(userEmail, orgIdentifier, acceptOrganizationInvite,
            includeAccountKeys, includeLegacyKeys));
    }

    // Salt equals the email because Stage 1 (PM-27044) requires salt == email on initial set.
    private static Dictionary<string, object?> CreateModernRequest(string userEmail, string orgIdentifier,
        bool? acceptOrganizationInvite, bool includeAccountKeys, bool includeLegacyKeys = false)
    {
        var kdf = new { kdfType = (int)KdfType.PBKDF2_SHA256, iterations = 600000 };
        var request = new Dictionary<string, object?>
        {
            ["masterPasswordAuthentication"] = new
            {
                kdf,
                masterPasswordAuthenticationHash = _newMasterPasswordHash,
                salt = userEmail
            },
            ["masterPasswordUnlock"] = new
            {
                kdf,
                masterKeyWrappedUserKey = _masterKeyWrappedUserKey,
                salt = userEmail
            },
            ["masterPasswordHint"] = _hint,
            ["orgIdentifier"] = orgIdentifier,
        };

        if (includeAccountKeys)
        {
            request["accountKeys"] = new
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
            };
        }

        if (includeLegacyKeys)
        {
            request["keys"] = new { publicKey = "v1-publicKey", encryptedPrivateKey = "v1-encryptedPrivateKey" };
        }

        AddAcceptOrganizationInvite(request, acceptOrganizationInvite);

        return request;
    }
}
