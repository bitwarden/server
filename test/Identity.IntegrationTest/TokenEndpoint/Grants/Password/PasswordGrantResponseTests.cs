using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.Repositories;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.OrganizationTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.PasswordGrantTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenEndpointTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;
using static Bit.Identity.IntegrationTest.Helpers.UserTestHelpers;

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.Password;

/// <summary>
/// Integration tests for a successful <c>password</c> grant: the response body, the access token lifetime, and what a
/// login records for the user.
/// </summary>
[SutProviderCustomize]
public class PasswordGrantResponseTests
{
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_GrantTypePassword_Success(RegisterFinishRequestModel requestModel)
    {
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        using var localFactory = new IdentityApplicationFactory();
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        var context = await PostLoginAsync(localFactory.Server, user, requestModel.MasterPasswordHash);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var root = body.RootElement;
        var expectedFields = new[]
        {
            "access_token", "expires_in", "refresh_token", "scope", "token_type", "AccountKeys",
            "ForcePasswordReset", "Kdf", "KdfIterations", "KdfMemory", "KdfParallelism", "Key",
            "MasterPasswordPolicy", "PrivateKey", "UserDecryptionOptions",
        };
        Assert.Equal(expectedFields.Order(StringComparer.Ordinal),
            root.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        AssertRefreshTokenExists(root);
        AssertHelper.AssertJsonProperty(root, "ForcePasswordReset", JsonValueKind.False);
        var kdf = AssertHelper.AssertJsonProperty(root, "Kdf", JsonValueKind.Number).GetInt32();
        Assert.Equal((int)KdfType.PBKDF2_SHA256, kdf);
        var kdfIterations = AssertHelper.AssertJsonProperty(root, "KdfIterations", JsonValueKind.Number).GetInt32();
        Assert.Equal(KdfConstants.PBKDF2_ITERATIONS.Default, kdfIterations);
        AssertMasterPasswordDecryptionOptions(root,
            expectedKdfType: KdfType.PBKDF2_SHA256,
            expectedKdfIterations: KdfConstants.PBKDF2_ITERATIONS.Default,
            expectedMasterKeyEncryptedUserKey: requestModel.UserSymmetricKey!,
            expectedSalt: requestModel.Email);
        AssertDecryptionOptionFields(root, "HasMasterPassword", "MasterPasswordUnlock", "Object");
    }

    /// <summary>
    /// A login tells the client when the user must change the master password.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_ForcePasswordReset_IsReturned()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        Assert.False(user.ForcePasswordReset);
        await UpdateUserAsync(factory, user.Id, u => u.ForcePasswordReset = true);

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword);

        using var body = await AssertDefaultTokenBodyAsync(context);
        AssertHelper.AssertJsonProperty(body.RootElement, "ForcePasswordReset", JsonValueKind.True);
    }

    /// <summary>
    /// A login returns the KDF settings of the user, so the client can derive the master key.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_ReturnsUserKdfSettings()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        await UpdateUserAsync(factory, user.Id, u =>
        {
            u.Kdf = KdfType.Argon2id;
            u.KdfIterations = 3;
            u.KdfMemory = 64;
            u.KdfParallelism = 4;
        });

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var root = body.RootElement;
        Assert.Equal((int)KdfType.Argon2id,
            AssertHelper.AssertJsonProperty(root, "Kdf", JsonValueKind.Number).GetInt32());
        Assert.Equal(3, AssertHelper.AssertJsonProperty(root, "KdfIterations", JsonValueKind.Number).GetInt32());
        Assert.Equal(64, AssertHelper.AssertJsonProperty(root, "KdfMemory", JsonValueKind.Number).GetInt32());
        Assert.Equal(4, AssertHelper.AssertJsonProperty(root, "KdfParallelism", JsonValueKind.Number).GetInt32());
    }

    /// <summary>
    /// A login returns the master password policy of the organizations of the user, so the client can require a
    /// master password change at login.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_ReturnsOrganizationMasterPasswordPolicy()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        var membership = await CreateOrganizationWithConfirmedMemberAsync(factory, user.Id, OrganizationUserType.User);
        var policy = new Policy
        {
            OrganizationId = membership.OrganizationId,
            Type = PolicyType.MasterPassword,
            Enabled = true,
        };
        policy.SetDataModel(new MasterPasswordPolicyData { MinLength = 20, EnforceOnLogin = true });
        await factory.Services.GetRequiredService<IPolicyRepository>().CreateAsync(policy);

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var masterPasswordPolicy =
            AssertHelper.AssertJsonProperty(body.RootElement, "MasterPasswordPolicy", JsonValueKind.Object);
        Assert.Equal(20,
            AssertHelper.AssertJsonProperty(masterPasswordPolicy, "MinLength", JsonValueKind.Number).GetInt32());
        AssertHelper.AssertJsonProperty(masterPasswordPolicy, "EnforceOnLogin", JsonValueKind.True);
    }

    /// <summary>
    /// A login of a user that no master password policy applies to returns a master password policy with no
    /// requirements, so the client does not require a master password change at login. The response leaves out
    /// requirements that have no value.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_NoMasterPasswordPolicy_ReturnsPolicyWithoutRequirements()
    {
        using var factory = new IdentityApplicationFactory();
        await RegisterUserAsync(factory);

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var masterPasswordPolicy =
            AssertHelper.AssertJsonProperty(body.RootElement, "MasterPasswordPolicy", JsonValueKind.Object);
        Assert.Equal("masterPasswordPolicy",
            AssertHelper.AssertJsonProperty(masterPasswordPolicy, "Object", JsonValueKind.String).GetString());
        Assert.Equal(["Object"], masterPasswordPolicy.EnumerateObject().Select(p => p.Name));
    }

    /// <summary>
    /// A login access token has the built-in access token lifetime of the client that sent the request.
    /// </summary>
    [Theory]
    [InlineData(BitwardenClient.Web, 3600)] // 1 hour
    [InlineData(BitwardenClient.Browser, 3600)] // 1 hour
    [InlineData(BitwardenClient.Desktop, 3600)] // 1 hour
    [InlineData(BitwardenClient.Mobile, 3600)] // 1 hour
    [InlineData(BitwardenClient.Cli, 3600)] // 1 hour
    [InlineData(BitwardenClient.DirectoryConnector, 24 * 3600)]
    public async Task PasswordGrant_AccessTokenLifetime_MatchesClient(string clientId, int expectedExpiresIn)
    {
        using var factory = new IdentityApplicationFactory();
        await RegisterUserAsync(factory);

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword, clientId: clientId);

        using var body = await AssertDefaultTokenBodyAsync(context, expectedExpiresIn: expectedExpiresIn);
    }

    /// <summary>
    /// The access token lifetime setting applies to every interactive client. The Directory Connector keeps its own
    /// lifetime.
    /// </summary>
    [Theory]
    [InlineData(BitwardenClient.Web, 1200)] // 20 min
    [InlineData(BitwardenClient.Mobile, 1200)] // 20 min
    [InlineData(BitwardenClient.DirectoryConnector, 24 * 3600)]
    public async Task PasswordGrant_AccessTokenLifetimeSetting_AppliesToInteractiveClients(
        string clientId, int expectedExpiresIn)
    {
        using var factory = new IdentityApplicationFactory();
        factory.UpdateConfiguration("globalSettings:identityServer:accessTokenLifetimeSeconds", "1200"); // 20 min
        await RegisterUserAsync(factory);

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword, clientId: clientId);

        using var body = await AssertDefaultTokenBodyAsync(context, expectedExpiresIn: expectedExpiresIn);
    }

    /// <summary>
    /// A login logs a login event for the user.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_LogsLoginEvent()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        Assert.Equal(0, await CountUserEventsAsync(factory, user.Id, EventType.User_LoggedIn));

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword);

        using var body = await AssertDefaultTokenBodyAsync(context);
        Assert.Equal(1, await CountUserEventsAsync(factory, user.Id, EventType.User_LoggedIn));
    }

    /// <summary>
    /// A login resets the failed login count of the user to 0.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_ResetsFailedLoginCount()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        await UpdateUserAsync(factory, user.Id, u => u.FailedLoginCount = 2);

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword);

        using var body = await AssertDefaultTokenBodyAsync(context);
        var userAfter = await factory.Services.GetRequiredService<IUserRepository>().GetByIdAsync(user.Id);
        Assert.Equal(0, userAfter!.FailedLoginCount);
    }

    /// <summary>
    /// A login from a device that the user has not used before saves the device to the user.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_NewDevice_IsSavedToUser()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        var deviceRepository = factory.Services.GetRequiredService<IDeviceRepository>();
        Assert.Empty(await deviceRepository.GetManyByUserIdAsync(user.Id));

        var context = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword,
            deviceIdentifier: "new-device-identifier", deviceType: DeviceType.Android, deviceName: "android");

        using var body = await AssertDefaultTokenBodyAsync(context);
        var device = Assert.Single(await deviceRepository.GetManyByUserIdAsync(user.Id));
        Assert.Equal("new-device-identifier", device.Identifier);
        Assert.Equal(DeviceType.Android, device.Type);
        Assert.Equal("android", device.Name);
    }

    /// <summary>
    /// A login from a device that the user has used before does not save the device again.
    /// </summary>
    [Fact]
    public async Task PasswordGrant_KnownDevice_IsNotSavedAgain()
    {
        using var factory = new IdentityApplicationFactory();
        var user = await RegisterUserAsync(factory);
        var deviceRepository = factory.Services.GetRequiredService<IDeviceRepository>();
        var firstContext = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword);
        using var firstBody = await AssertDefaultTokenBodyAsync(firstContext);
        var device = Assert.Single(await deviceRepository.GetManyByUserIdAsync(user.Id));

        var secondContext = await factory.ContextFromPasswordAsync(DefaultUsername, DefaultPassword);

        using var secondBody = await AssertDefaultTokenBodyAsync(secondContext);
        Assert.Equal(device.Id, Assert.Single(await deviceRepository.GetManyByUserIdAsync(user.Id)).Id);
    }
}
