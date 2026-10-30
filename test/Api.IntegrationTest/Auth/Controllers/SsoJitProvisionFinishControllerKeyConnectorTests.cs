using System.Net;
using Bit.Api.Auth.Models.Request.SsoJitProvisionFinish;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.Auth.Controllers;

public class SsoJitProvisionFinishControllerKeyConnectorTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _endpoint = "/sso-jit-provision-finish/key-connector";
    private const string _mockEncryptedString =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";
    private const string _mockEncryptedType7String = "7.AOs41Hd8OQiCPXjyJKCiDA==";
    private const string _mockEncryptedType7WrappedSigningKey = "7.DRv74Kg1RSlFSam1MNFlGD==";
    private const string _mockKeyId = "0123456789abcdef0123456789abcdef";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IUserSignatureKeyPairRepository _userSignatureKeyPairRepository;
    private string _ownerEmail = null!;

    public SsoJitProvisionFinishControllerKeyConnectorTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _userRepository = _factory.GetService<IUserRepository>();
        _organizationRepository = _factory.GetService<IOrganizationRepository>();
        _organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
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

    [Fact]
    public async Task FinishKeyConnectorAsync_NotLoggedIn_Unauthorized()
    {
        var request = new SsoJitProvisionFinishSetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeys = CreateV2Keys("test-org"),
            AcceptOrganizationInvite = false
        };

        var response = await _client.PostAsJsonAsync(_endpoint, request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(false, OrganizationUserStatusType.Invited)]
    [InlineData(true, OrganizationUserStatusType.Accepted)]
    public async Task FinishKeyConnectorAsync_V2_SetsKeysAndAppliesAcceptFlag(bool acceptOrganizationInvite,
        OrganizationUserStatusType expectedStatus)
    {
        var orgSsoIdentifier = $"sso-{Guid.NewGuid()}";
        var (ssoUserEmail, organization) = await SetupKeyConnectorUserAsync(orgSsoIdentifier);

        var request = new SsoJitProvisionFinishSetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeys = CreateV2Keys(orgSsoIdentifier),
            AcceptOrganizationInvite = acceptOrganizationInvite
        };

        var response = await _client.PostAsJsonAsync(_endpoint, request);
        response.EnsureSuccessStatusCode();

        var user = await _userRepository.GetByEmailAsync(ssoUserEmail);
        Assert.NotNull(user);
        Assert.True(user.UsesKeyConnector);
        Assert.Null(user.MasterPassword);
        Assert.Equal(_mockEncryptedString, user.Key);
        Assert.Equal(_mockKeyId, user.UserKeyId);
        Assert.Equal("publicKey", user.PublicKey);
        Assert.Equal(_mockEncryptedType7String, user.PrivateKey);
        Assert.Equal("signedPublicKey", user.SignedPublicKey);
        Assert.Equal("v2", user.SecurityState);
        Assert.Equal(2, user.SecurityVersion);
        Assert.Equal(DateTime.UtcNow, user.RevisionDate, TimeSpan.FromMinutes(1));
        Assert.Equal(DateTime.UtcNow, user.AccountRevisionDate, TimeSpan.FromMinutes(1));

        var signatureKeyPair = await _userSignatureKeyPairRepository.GetByUserIdAsync(user.Id);
        Assert.NotNull(signatureKeyPair);
        Assert.Equal(SignatureAlgorithm.Ed25519, signatureKeyPair.SignatureAlgorithm);
        Assert.Equal(_mockEncryptedType7WrappedSigningKey, signatureKeyPair.WrappedSigningKey);
        Assert.Equal("verifyingKey", signatureKeyPair.VerifyingKey);

        await AssertOrganizationUserStatusAsync(organization, user, expectedStatus);
    }

    [Theory]
    [InlineData(false, OrganizationUserStatusType.Invited)]
    [InlineData(true, OrganizationUserStatusType.Accepted)]
    public async Task FinishKeyConnectorAsync_V1_SetsKeysAndAppliesAcceptFlag(bool acceptOrganizationInvite,
        OrganizationUserStatusType expectedStatus)
    {
        var orgSsoIdentifier = $"sso-{Guid.NewGuid()}";
        var (ssoUserEmail, organization) = await SetupKeyConnectorUserAsync(orgSsoIdentifier);

        var request = new SsoJitProvisionFinishSetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeys = new SetKeyConnectorKeyRequestModel
            {
                Key = _mockEncryptedString,
                Keys = new KeysRequestModel { PublicKey = "publicKey", EncryptedPrivateKey = _mockEncryptedString },
                Kdf = KdfType.PBKDF2_SHA256,
                KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
                OrgIdentifier = orgSsoIdentifier
            },
            AcceptOrganizationInvite = acceptOrganizationInvite
        };

        var response = await _client.PostAsJsonAsync(_endpoint, request);
        response.EnsureSuccessStatusCode();

        var user = await _userRepository.GetByEmailAsync(ssoUserEmail);
        Assert.NotNull(user);
        Assert.True(user.UsesKeyConnector);
        Assert.Null(user.MasterPassword);
        Assert.Equal(_mockEncryptedString, user.Key);
        Assert.Equal("publicKey", user.PublicKey);
        Assert.Equal(_mockEncryptedString, user.PrivateKey);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        Assert.Equal(KdfConstants.PBKDF2_ITERATIONS.Default, user.KdfIterations);
        Assert.Null(user.SignedPublicKey);
        Assert.Null(user.SecurityState);
        Assert.Null(await _userSignatureKeyPairRepository.GetByUserIdAsync(user.Id));
        Assert.Equal(DateTime.UtcNow, user.RevisionDate, TimeSpan.FromMinutes(1));
        Assert.Equal(DateTime.UtcNow, user.AccountRevisionDate, TimeSpan.FromMinutes(1));

        await AssertOrganizationUserStatusAsync(organization, user, expectedStatus);
    }

    [Fact]
    public async Task FinishKeyConnectorAsync_AcceptOrganizationInviteMissing_BadRequest()
    {
        var orgSsoIdentifier = $"sso-{Guid.NewGuid()}";
        var (ssoUserEmail, organization) = await SetupKeyConnectorUserAsync(orgSsoIdentifier);

        // Anonymous type so the flag is absent from the JSON body, not just false.
        var response = await _client.PostAsJsonAsync(_endpoint,
            new { KeyConnectorKeys = CreateV2Keys(orgSsoIdentifier) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("acceptOrganizationInvite", body, StringComparison.OrdinalIgnoreCase);
        await AssertNotProvisionedAsync(ssoUserEmail, organization);
    }

    [Fact]
    public async Task FinishKeyConnectorAsync_InvalidNestedV1Keys_BadRequest()
    {
        var orgSsoIdentifier = $"sso-{Guid.NewGuid()}";
        var (ssoUserEmail, organization) = await SetupKeyConnectorUserAsync(orgSsoIdentifier);

        // V1 shape without Keys or KDF: SetKeyConnectorKeyRequestModel.Validate() must reject it.
        var request = new SsoJitProvisionFinishSetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeys = new SetKeyConnectorKeyRequestModel
            {
                Key = _mockEncryptedString,
                OrgIdentifier = orgSsoIdentifier
            },
            AcceptOrganizationInvite = true
        };

        var response = await _client.PostAsJsonAsync(_endpoint, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNotProvisionedAsync(ssoUserEmail, organization);
    }

    [Fact]
    public async Task FinishKeyConnectorAsync_UserNotInOrganization_BadRequestAndNoKeysSet()
    {
        var orgSsoIdentifier = $"sso-{Guid.NewGuid()}";
        await SetupKeyConnectorOrganizationAsync(orgSsoIdentifier);

        var nonMemberEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(nonMemberEmail);
        await _loginHelper.LoginAsync(nonMemberEmail);
        await SimulateJitProvisionedUserAsync(nonMemberEmail);

        var request = new SsoJitProvisionFinishSetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeys = CreateV2Keys(orgSsoIdentifier),
            AcceptOrganizationInvite = false
        };

        var response = await _client.PostAsJsonAsync(_endpoint, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertJitProvisionedStateAsync(nonMemberEmail);
    }

    private async Task<Organization> SetupKeyConnectorOrganizationAsync(string orgSsoIdentifier)
    {
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            PlanType.EnterpriseAnnually, _ownerEmail, passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);
        organization.UseKeyConnector = true;
        organization.UseSso = true;
        organization.Identifier = orgSsoIdentifier;
        await _organizationRepository.ReplaceAsync(organization);
        return organization;
    }

    private async Task<(string, Organization)> SetupKeyConnectorUserAsync(string orgSsoIdentifier)
    {
        var organization = await SetupKeyConnectorOrganizationAsync(orgSsoIdentifier);

        var ssoUserEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ssoUserEmail);
        await _loginHelper.LoginAsync(ssoUserEmail);

        await OrganizationTestHelpers.CreateUserAsync(_factory, organization.Id, ssoUserEmail,
            OrganizationUserType.User, userStatusType: OrganizationUserStatusType.Invited);

        await SimulateJitProvisionedUserAsync(ssoUserEmail);

        return (ssoUserEmail, organization);
    }

    /// <summary>
    /// LoginWithNewAccount registers a full account. A JIT provisioned SSO user has no master password,
    /// user key, or account keys yet, so clear them to match that state.
    /// </summary>
    private async Task SimulateJitProvisionedUserAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        Assert.NotNull(user);
        user.MasterPassword = null;
        user.Key = null;
        user.PrivateKey = null;
        user.PublicKey = null;
        user.SignedPublicKey = null;
        await _userRepository.ReplaceAsync(user);

        await AssertJitProvisionedStateAsync(email);
    }

    private async Task AssertJitProvisionedStateAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        Assert.NotNull(user);
        Assert.False(user.UsesKeyConnector);
        Assert.Null(user.MasterPassword);
        Assert.Null(user.Key);
        Assert.Null(user.UserKeyId);
        Assert.Null(user.PublicKey);
        Assert.Null(user.PrivateKey);
        Assert.Null(user.SignedPublicKey);
        Assert.Null(user.SecurityState);
        Assert.Null(await _userSignatureKeyPairRepository.GetByUserIdAsync(user.Id));
    }

    private async Task AssertOrganizationUserStatusAsync(Organization organization, User user,
        OrganizationUserStatusType expectedStatus)
    {
        var organizationUser = await _organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id);
        Assert.NotNull(organizationUser);
        Assert.Equal(expectedStatus, organizationUser.Status);
        Assert.Equal(user.Id, organizationUser.UserId);
    }

    private async Task AssertNotProvisionedAsync(string ssoUserEmail, Organization organization)
    {
        await AssertJitProvisionedStateAsync(ssoUserEmail);
        var user = await _userRepository.GetByEmailAsync(ssoUserEmail);
        Assert.NotNull(user);
        await AssertOrganizationUserStatusAsync(organization, user, OrganizationUserStatusType.Invited);
    }

    private static SetKeyConnectorKeyRequestModel CreateV2Keys(string orgSsoIdentifier) =>
        new()
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
            ContainedKeyId = _mockKeyId,
            OrgIdentifier = orgSsoIdentifier
        };
}
