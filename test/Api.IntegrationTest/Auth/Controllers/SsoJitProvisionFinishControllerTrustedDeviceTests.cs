using System.Net;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.IntegrationTestCommon.Factories;
using Xunit;

namespace Bit.Api.IntegrationTest.Auth.Controllers;

public class SsoJitProvisionFinishControllerTrustedDeviceTests
    : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private const string _route = "sso-jit-provision-finish/trusted-device";
    private const string _resetPasswordKey = "4.YWJjZA==";
    private const string _wrappedPrivateKey = "7.AOs41Hd8OQiCPXjyJKCiDA==";
    private const string _wrappedSigningKey = "7.DRv74Kg1RSlFSam1MNFlGD==";
    private const string _deviceEncryptedUserKey = "4.ZGV2aWNlLXVzZXIta2V5";
    private const string _deviceEncryptedPublicKey = "7.ZGV2aWNlLXB1YmxpYy1rZXk=";
    private const string _deviceEncryptedPrivateKey =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;

    public SsoJitProvisionFinishControllerTrustedDeviceTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        var ownerEmail = $"tde-jit-owner-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually, ownerEmail: ownerEmail,
            passwordManagerSeats: 5, paymentMethod: PaymentMethodType.Card);

        _organization.UseSso = true;
        _organization.UseResetPassword = true;
        _organization.UsePolicies = true;
        await _factory.GetService<IOrganizationRepository>().ReplaceAsync(_organization);

        await _factory.GetService<IPolicyRepository>().CreateAsync(new Policy
        {
            OrganizationId = _organization.Id,
            Type = PolicyType.ResetPassword,
            Enabled = true,
            Data = "{}"
        });

        await _factory.GetService<ISsoConfigRepository>().CreateAsync(new SsoConfig
        {
            OrganizationId = _organization.Id,
            Enabled = true,
            Data = JsonSerializer.Serialize(new SsoConfigurationData
            {
                MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption,
            }, JsonHelpers.CamelCase),
        });
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task FinishTrustedDevice_OpenInvite_StoresKeysAndTrustsDeviceButLeavesMemberInvited()
    {
        var (user, orgUser, device) = await CreateJitProvisionedMemberAsync();

        var response = await _client.PostAsJsonAsync(_route,
            CreateRequest(includeDeviceKeys: true, acceptOrganizationInvite: false));

        await AssertSuccessAsync(response);

        var updatedUser = await _factory.GetService<IUserRepository>().GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal("publicKey", updatedUser.PublicKey);
        Assert.Equal(_wrappedPrivateKey, updatedUser.PrivateKey);
        Assert.Equal("signedPublicKey", updatedUser.SignedPublicKey);
        Assert.Equal(2, updatedUser.SecurityVersion);

        await AssertDeviceTrustedAsync(device.Id);

        var updatedOrgUser = await _factory.GetService<IOrganizationUserRepository>().GetByIdAsync(orgUser.Id);
        Assert.NotNull(updatedOrgUser);
        Assert.Null(updatedOrgUser.ResetPasswordKey);
        Assert.Equal(OrganizationUserStatusType.Invited, updatedOrgUser.Status);
    }

    [Fact]
    public async Task FinishTrustedDevice_DirectInvite_EnrollsInAccountRecoveryAndAcceptsMember()
    {
        var (user, orgUser, device) = await CreateJitProvisionedMemberAsync();

        var response = await _client.PostAsJsonAsync(_route,
            CreateRequest(includeDeviceKeys: true, acceptOrganizationInvite: true));

        await AssertSuccessAsync(response);

        var updatedUser = await _factory.GetService<IUserRepository>().GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal("publicKey", updatedUser.PublicKey);

        await AssertDeviceTrustedAsync(device.Id);

        var updatedOrgUser = await _factory.GetService<IOrganizationUserRepository>().GetByIdAsync(orgUser.Id);
        Assert.NotNull(updatedOrgUser);
        Assert.Equal(_resetPasswordKey, updatedOrgUser.ResetPasswordKey);
        Assert.Equal(OrganizationUserStatusType.Accepted, updatedOrgUser.Status);
    }

    [Fact]
    public async Task FinishTrustedDevice_NoDeviceKeys_LeavesDeviceUntrusted()
    {
        var (user, _, device) = await CreateJitProvisionedMemberAsync();

        var response = await _client.PostAsJsonAsync(_route,
            CreateRequest(includeDeviceKeys: false, acceptOrganizationInvite: false));

        await AssertSuccessAsync(response);

        var updatedUser = await _factory.GetService<IUserRepository>().GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal("publicKey", updatedUser.PublicKey);

        var updatedDevice = await _factory.GetService<IDeviceRepository>().GetByIdAsync(device.Id);
        Assert.NotNull(updatedDevice);
        Assert.Null(updatedDevice.EncryptedUserKey);
        Assert.Null(updatedDevice.EncryptedPublicKey);
        Assert.Null(updatedDevice.EncryptedPrivateKey);
    }

    [Fact]
    public async Task FinishTrustedDevice_UserHasExistingKeypair_ReturnsBadRequestAndChangesNothing()
    {
        var (user, orgUser, device) = await CreateJitProvisionedMemberAsync(keepExistingKeypair: true);

        var response = await _client.PostAsJsonAsync(_route,
            CreateRequest(includeDeviceKeys: true, acceptOrganizationInvite: true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var updatedUser = await _factory.GetService<IUserRepository>().GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(user.PublicKey, updatedUser.PublicKey);
        Assert.Null(updatedUser.SignedPublicKey);

        var updatedDevice = await _factory.GetService<IDeviceRepository>().GetByIdAsync(device.Id);
        Assert.NotNull(updatedDevice);
        Assert.Null(updatedDevice.EncryptedUserKey);

        var updatedOrgUser = await _factory.GetService<IOrganizationUserRepository>().GetByIdAsync(orgUser.Id);
        Assert.NotNull(updatedOrgUser);
        Assert.Null(updatedOrgUser.ResetPasswordKey);
        Assert.Equal(OrganizationUserStatusType.Invited, updatedOrgUser.Status);
    }

    [Fact]
    public async Task FinishTrustedDevice_AcceptOrganizationInviteMissing_ReturnsBadRequestAndChangesNothing()
    {
        var (user, orgUser, _) = await CreateJitProvisionedMemberAsync();

        var response = await _client.PostAsJsonAsync(_route,
            CreateRequest(includeDeviceKeys: true, acceptOrganizationInvite: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("acceptOrganizationInvite", body, StringComparison.OrdinalIgnoreCase);

        var updatedUser = await _factory.GetService<IUserRepository>().GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Null(updatedUser.PublicKey);

        var updatedOrgUser = await _factory.GetService<IOrganizationUserRepository>().GetByIdAsync(orgUser.Id);
        Assert.NotNull(updatedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Invited, updatedOrgUser.Status);
    }

    /// <summary>
    /// Creates an Invited member that looks like a fresh SSO JIT user: no master password, no keys, and an untrusted
    /// login device. The access token carries that device's identifier, which the endpoint trusts.
    /// </summary>
    private async Task<(User User, OrganizationUser OrgUser, Device Device)> CreateJitProvisionedMemberAsync(
        bool keepExistingKeypair = false)
    {
        var (email, orgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);

        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();
        orgUser.Status = OrganizationUserStatusType.Invited;
        await organizationUserRepository.ReplaceAsync(orgUser);

        await _loginHelper.LoginAsync(email);

        var userRepository = _factory.GetService<IUserRepository>();
        var user = await userRepository.GetByEmailAsync(email);
        Assert.NotNull(user);
        user.MasterPassword = null;
        user.Key = null;
        if (!keepExistingKeypair)
        {
            user.PublicKey = null;
            user.PrivateKey = null;
        }
        await userRepository.ReplaceAsync(user);

        var device = await _factory.GetService<IDeviceRepository>()
            .GetByIdentifierAsync(IdentityApplicationFactory.DefaultDeviceIdentifier, user.Id);
        Assert.NotNull(device);
        Assert.Null(device.EncryptedUserKey);

        return (user, orgUser, device);
    }

    private Dictionary<string, object?> CreateRequest(bool includeDeviceKeys, bool? acceptOrganizationInvite)
    {
        var request = new Dictionary<string, object?>
        {
            ["keys"] = new
            {
                publicKey = "publicKey",
                encryptedPrivateKey = _wrappedPrivateKey,
                accountKeys = new
                {
                    userKeyEncryptedAccountPrivateKey = _wrappedPrivateKey,
                    accountPublicKey = "publicKey",
                    publicKeyEncryptionKeyPair = new
                    {
                        wrappedPrivateKey = _wrappedPrivateKey,
                        publicKey = "publicKey",
                        signedPublicKey = "signedPublicKey"
                    },
                    signatureKeyPair = new
                    {
                        signatureAlgorithm = "ed25519",
                        wrappedSigningKey = _wrappedSigningKey,
                        verifyingKey = "verifyingKey"
                    },
                    securityState = new { securityVersion = 2, securityState = "v2" }
                }
            },
            ["organizationId"] = _organization.Id,
            ["resetPasswordKey"] = _resetPasswordKey,
        };

        if (includeDeviceKeys)
        {
            request["deviceKeys"] = new
            {
                encryptedUserKey = _deviceEncryptedUserKey,
                encryptedPublicKey = _deviceEncryptedPublicKey,
                encryptedPrivateKey = _deviceEncryptedPrivateKey
            };
        }

        if (acceptOrganizationInvite.HasValue)
        {
            request["acceptOrganizationInvite"] = acceptOrganizationInvite.Value;
        }

        return request;
    }

    private async Task AssertDeviceTrustedAsync(Guid deviceId)
    {
        var updatedDevice = await _factory.GetService<IDeviceRepository>().GetByIdAsync(deviceId);
        Assert.NotNull(updatedDevice);
        Assert.Equal(_deviceEncryptedUserKey, updatedDevice.EncryptedUserKey);
        Assert.Equal(_deviceEncryptedPublicKey, updatedDevice.EncryptedPublicKey);
        Assert.Equal(_deviceEncryptedPrivateKey, updatedDevice.EncryptedPrivateKey);
    }

    private static async Task AssertSuccessAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected success but got {response.StatusCode}. Error: {content}");
        }
    }
}
