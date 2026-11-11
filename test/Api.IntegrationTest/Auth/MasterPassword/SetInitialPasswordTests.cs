using System.Net;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;
using static Bit.Api.IntegrationTest.Auth.Helpers.MasterPasswordPayloads;
using static Bit.Core.KeyManagement.Enums.SignatureAlgorithm;

namespace Bit.Api.IntegrationTest.Auth.MasterPassword;

public class SetInitialPasswordTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private static readonly string _mockEncryptedType7String = "7.AOs41Hd8OQiCPXjyJKCiDA==";
    private static readonly string _mockEncryptedType7WrappedSigningKey = "7.DRv74Kg1RSlFSam1MNFlGD==";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly Bitwarden.Server.Sdk.Features.IFeatureService _featureService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly IUserSignatureKeyPairRepository _userSignatureKeyPairRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public SetInitialPasswordTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<Bitwarden.Server.Sdk.Features.IFeatureService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _featureService = _factory.GetService<Bitwarden.Server.Sdk.Features.IFeatureService>();
        _passwordHasher = _factory.GetService<IPasswordHasher<User>>();
        _organizationRepository = _factory.GetService<IOrganizationRepository>();
        _ssoConfigRepository = _factory.GetService<ISsoConfigRepository>();
        _userSignatureKeyPairRepository = _factory.GetService<IUserSignatureKeyPairRepository>();
        _eventRepository = _factory.GetService<IEventRepository>();
        _organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
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
            masterPasswordHash = NewMasterPasswordHash,
            key = MasterKeyWrappedUserKey,
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
        var verificationResult = _passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.MasterPassword, NewMasterPasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, verificationResult);

        // Verify KDF settings
        Assert.Equal(KdfType.PBKDF2_SHA256, updatedUser.Kdf);
        Assert.Equal(600_000, updatedUser.KdfIterations);
        Assert.Null(updatedUser.KdfMemory);
        Assert.Null(updatedUser.KdfParallelism);

        // MasterPasswordSalt column must never be null/empty after a successful set-password.
        // Legacy V1 path falls back to the email-derived V1 salt.
        Assert.Equal(userEmail, updatedUser.MasterPasswordSalt);

        // Verify timestamps are updated
        Assert.Equal(DateTime.UtcNow, updatedUser.RevisionDate, TimeSpan.FromMinutes(1));
        Assert.Equal(DateTime.UtcNow, updatedUser.AccountRevisionDate, TimeSpan.FromMinutes(1));

        // Verify keys are set (V1 uses Keys property)
        Assert.Equal(MasterKeyWrappedUserKey, updatedUser.Key);
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

    // Modern client + V1 encryption: request carries MPAD + MPUD + legacy Keys (no AccountKeys),
    // V2 MP JIT flag OFF → request routes to the V1 command. KDF/Key/Salt come from MPUD, keypair
    // from legacy Keys.
    [Theory]
    [BitAutoData]
    public async Task PostSetPasswordAsync_V1_WithMpadMpud_MasterPasswordDecryption_Success(string organizationSsoIdentifier)
    {
        // Arrange - Create organization and user
        var ownerEmail = $"owner-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail);

        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            ownerEmail: ownerEmail,
            name: "Test Org V1 modern");
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

        // Modern V1 MP JIT request shape: MPAD + MPUD + legacy Keys, no AccountKeys.
        // V2 MP JIT flag is left OFF (default mock behavior), so this routes to the V1 command.
        // Salt must equal the email during Stage 1 (PM-27044) — ValidateSaltUnchangedForUser rejects
        // divergent salts until clients consume the explicit salt from prelogin (Stage 3 / PM-28143).
        var request = new
        {
            masterPasswordAuthentication = new
            {
                kdf = new
                {
                    kdfType = (int)KdfType.PBKDF2_SHA256,
                    iterations = 600000
                },
                masterPasswordAuthenticationHash = NewMasterPasswordHash,
                salt = userEmail
            },
            masterPasswordUnlock = new
            {
                kdf = new
                {
                    kdfType = (int)KdfType.PBKDF2_SHA256,
                    iterations = 600000
                },
                masterKeyWrappedUserKey = MasterKeyWrappedUserKey,
                salt = userEmail
            },
            keys = new
            {
                publicKey = "modern-v1-publicKey",
                encryptedPrivateKey = "modern-v1-encryptedPrivateKey"
            },
            masterPasswordHint = "modern-v1-integration-test-hint",
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
        Assert.Equal("modern-v1-integration-test-hint", updatedUser.MasterPasswordHint);

        // Verify the master password is hashed and stored
        Assert.NotNull(updatedUser.MasterPassword);
        var verificationResult = _passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.MasterPassword, NewMasterPasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, verificationResult);

        // Verify KDF settings — ToUser reads from MPUD on the modern shape
        Assert.Equal(KdfType.PBKDF2_SHA256, updatedUser.Kdf);
        Assert.Equal(600_000, updatedUser.KdfIterations);
        Assert.Null(updatedUser.KdfMemory);
        Assert.Null(updatedUser.KdfParallelism);

        // MasterPasswordSalt column must never be null/empty after a successful set-password.
        // Modern V1 MP JIT path persists the MPUD-provided salt.
        var expectedSalt = request.masterPasswordUnlock.salt;
        Assert.Equal(expectedSalt, updatedUser.MasterPasswordSalt);

        // Verify timestamps are updated
        Assert.Equal(DateTime.UtcNow, updatedUser.RevisionDate, TimeSpan.FromMinutes(1));
        Assert.Equal(DateTime.UtcNow, updatedUser.AccountRevisionDate, TimeSpan.FromMinutes(1));

        // user.Key from MPUD; keypair from legacy Keys
        Assert.Equal(MasterKeyWrappedUserKey, updatedUser.Key);
        Assert.Equal("modern-v1-publicKey", updatedUser.PublicKey);
        Assert.Equal("modern-v1-encryptedPrivateKey", updatedUser.PrivateKey);

        // V2-only fields must NOT be set on the V1 path
        Assert.Null(updatedUser.SignedPublicKey);

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

    // V1 TDE user (no V2 cryptographic state) sets their initial password via the TDE command.
    // Modern TDE request shape: MPAD + MPUD + no keys. The V2RegistrationTDEJIT flag governs SSO+TDE
    // account creation, not set-password — so this routes to _tdeSetPasswordCommand regardless of
    // any flag state. The command sets the master password without mutating the existing keypair.
    [Theory]
    [BitAutoData]
    public async Task PostSetPasswordAsync_TDE_V1User_Success(string organizationSsoIdentifier)
    {
        // Arrange - Create organization with TDE
        var ownerEmail = $"owner-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail);

        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            ownerEmail: ownerEmail,
            name: "Test Org TDE V1 modern");
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

        // Set up TDE user without master password but with an existing keypair (no V2 state — this is
        // the pre-V2-TDE-flag world). The TDE command must leave PublicKey/PrivateKey intact.
        user.MasterPassword = null;
        user.Key = null;
        user.PublicKey = "tde-v1-publicKey";
        user.PrivateKey = _mockEncryptedType7String;
        await _userRepository.ReplaceAsync(user);

        // Modern TDE request shape: MPAD + MPUD, no AccountKeys (and no Keys).
        // Routes to the TDE command (no feature-flag gate; V2RegistrationTDEJIT is for SSO+TDE registration).
        var jsonRequest = CreateV2SetPasswordRequestJson(
            userEmail,
            organization.Identifier,
            "modern-v1-tde-test-hint",
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
        Assert.Equal("modern-v1-tde-test-hint", updatedUser.MasterPasswordHint);

        // Verify the master password is hashed and stored
        Assert.NotNull(updatedUser.MasterPassword);
        var verificationResult = _passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.MasterPassword, NewMasterPasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, verificationResult);

        // Verify KDF settings persisted from MPUD
        Assert.Equal(KdfType.PBKDF2_SHA256, updatedUser.Kdf);
        Assert.Equal(600_000, updatedUser.KdfIterations);
        Assert.Null(updatedUser.KdfMemory);
        Assert.Null(updatedUser.KdfParallelism);

        // MasterPasswordSalt column must never be null/empty after a successful set-password.
        // TDE command persists the MPUD-provided salt (CreateV2SetPasswordRequestJson sets salt = userEmail).
        Assert.Equal(userEmail, updatedUser.MasterPasswordSalt);

        // Verify timestamps are updated
        Assert.Equal(DateTime.UtcNow, updatedUser.RevisionDate, TimeSpan.FromMinutes(1));
        Assert.Equal(DateTime.UtcNow, updatedUser.AccountRevisionDate, TimeSpan.FromMinutes(1));

        // user.Key from MPUD; existing keypair preserved (TDE command does not mutate PublicKey/PrivateKey)
        Assert.Equal(MasterKeyWrappedUserKey, updatedUser.Key);
        Assert.Equal("tde-v1-publicKey", updatedUser.PublicKey);
        Assert.Equal(_mockEncryptedType7String, updatedUser.PrivateKey);

        // V2 cryptographic state must NOT be set (TDE command doesn't touch it; this is a V1 TDE user)
        Assert.Null(updatedUser.SignedPublicKey);

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

    [Theory]
    [BitAutoData]
    public async Task PostSetPasswordAsync_V2_MasterPasswordDecryption_Success(string organizationSsoIdentifier)
    {
        _featureService.IsEnabled(FeatureFlagKeys.EnableAccountEncryptionV2JitPasswordRegistration).Returns(true);

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
        var verificationResult = _passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.MasterPassword, NewMasterPasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, verificationResult);

        // Verify KDF settings
        Assert.Equal(KdfType.PBKDF2_SHA256, updatedUser.Kdf);
        Assert.Equal(600_000, updatedUser.KdfIterations);
        Assert.Null(updatedUser.KdfMemory);
        Assert.Null(updatedUser.KdfParallelism);

        // MasterPasswordSalt column must never be null/empty after a successful set-password.
        // V2 MP JIT path persists the MPUD-provided salt (the helper sends userEmail as the salt value).
        Assert.Equal(userEmail, updatedUser.MasterPasswordSalt);

        // Verify timestamps are updated
        Assert.Equal(DateTime.UtcNow, updatedUser.RevisionDate, TimeSpan.FromMinutes(1));
        Assert.Equal(DateTime.UtcNow, updatedUser.AccountRevisionDate, TimeSpan.FromMinutes(1));

        // Verify keys are set
        Assert.Equal(MasterKeyWrappedUserKey, updatedUser.Key);
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
        var verificationResult = _passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.MasterPassword, NewMasterPasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, verificationResult);

        // Verify KDF settings
        Assert.Equal(KdfType.PBKDF2_SHA256, updatedUser.Kdf);
        Assert.Equal(600_000, updatedUser.KdfIterations);
        Assert.Null(updatedUser.KdfMemory);
        Assert.Null(updatedUser.KdfParallelism);

        // MasterPasswordSalt column must never be null/empty after a successful set-password.
        // V2 TDE path persists the MPUD-provided salt (the helper sends userEmail as the salt value).
        Assert.Equal(userEmail, updatedUser.MasterPasswordSalt);

        // Verify timestamps are updated
        Assert.Equal(DateTime.UtcNow, updatedUser.RevisionDate, TimeSpan.FromMinutes(1));
        Assert.Equal(DateTime.UtcNow, updatedUser.AccountRevisionDate, TimeSpan.FromMinutes(1));

        // Verify key is set
        Assert.Equal(MasterKeyWrappedUserKey, updatedUser.Key);

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
                masterPasswordAuthenticationHash = NewMasterPasswordHash,
                salt = email
            },
            masterPasswordUnlock = new
            {
                kdf = new
                {
                    kdfType = 0,
                    iterations = 650000  // Different from authentication KDF
                },
                masterKeyWrappedUserKey = MasterKeyWrappedUserKey,
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

    // Salt is set to userEmail because during Stage 1 (PM-27044) the server requires
    // salt == email. SetInitialPasswordData.ValidateDataForUser rejects divergent salts
    // until Stage 3 (PM-28143).
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
                masterPasswordAuthenticationHash = NewMasterPasswordHash,
                salt = userEmail
            },
            masterPasswordUnlock = new
            {
                kdf,
                masterKeyWrappedUserKey = MasterKeyWrappedUserKey,
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
