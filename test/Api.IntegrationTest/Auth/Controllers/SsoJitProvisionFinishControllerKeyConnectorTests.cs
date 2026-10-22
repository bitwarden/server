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
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.KeyManagement.Models.Api.Request;
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
        Assert.Equal(_mockEncryptedString, user.Key);
        Assert.True(user.UsesKeyConnector);
        Assert.Equal(_mockKeyId, user.UserKeyId);
        Assert.Equal("signedPublicKey", user.SignedPublicKey);
        Assert.Equal("v2", user.SecurityState);
        Assert.Equal(2, user.SecurityVersion);

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
        var ssoUser = await _userRepository.GetByEmailAsync(ssoUserEmail);
        Assert.NotNull(ssoUser);

        var request = new SsoJitProvisionFinishSetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeys = new SetKeyConnectorKeyRequestModel
            {
                Key = _mockEncryptedString,
                Keys = new KeysRequestModel { PublicKey = ssoUser.PublicKey, EncryptedPrivateKey = ssoUser.PrivateKey },
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
        Assert.Equal(_mockEncryptedString, user.Key);
        Assert.True(user.UsesKeyConnector);
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

        var request = new SsoJitProvisionFinishSetKeyConnectorKeyRequestModel
        {
            KeyConnectorKeys = CreateV2Keys(orgSsoIdentifier),
            AcceptOrganizationInvite = false
        };

        var response = await _client.PostAsJsonAsync(_endpoint, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var user = await _userRepository.GetByEmailAsync(nonMemberEmail);
        Assert.NotNull(user);
        Assert.False(user.UsesKeyConnector);
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

        return (ssoUserEmail, organization);
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
        var user = await _userRepository.GetByEmailAsync(ssoUserEmail);
        Assert.NotNull(user);
        Assert.False(user.UsesKeyConnector);
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
