using System.Text.Json;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.IntegrationTestCommon;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Constants;
using Bit.Test.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.SsoLoginTestHelpers;

#nullable enable

namespace Bit.Identity.IntegrationTest.TokenEndpoint.DecryptionOptions;

public class SsoLoginTrustedDeviceDecryptionOptionTests
{
    [Fact]
    public async Task SsoLogin_TrustedDeviceEncryption_ReturnsOptions()
    {
        // Arrange
        using var responseBody = await RunSuccessTestAsync(MemberDecryptionType.TrustedDeviceEncryption);

        // Assert
        // If the organization has selected TrustedDeviceEncryption but the user still has their master password
        // they can decrypt with either option
        var root = responseBody.RootElement;
        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);
        var userDecryptionOptions = AssertHelper.AssertJsonProperty(root, "UserDecryptionOptions", JsonValueKind.Object);

        // Expected to look like:
        // "UserDecryptionOptions": {
        //   "Object": "userDecryptionOptions"
        //   "HasMasterPassword": true,
        //   "TrustedDeviceOption": {
        //     "HasAdminApproval": false
        //   }
        // }

        // Should have master password & one for trusted device with admin approval
        AssertHelper.AssertJsonProperty(userDecryptionOptions, "HasMasterPassword", JsonValueKind.True);

        var trustedDeviceOption = AssertHelper.AssertJsonProperty(userDecryptionOptions, "TrustedDeviceOption", JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(trustedDeviceOption, "HasAdminApproval", JsonValueKind.False);
    }

    [Fact]
    public async Task SsoLogin_TrustedDeviceEncryption_WithAdminResetPolicy_ReturnsOptions()
    {
        // Arrange
        using var responseBody = await RunSuccessTestAsync(async factory =>
        {
            var database = factory.GetDatabaseContext();

            var organization = await database.Organizations.SingleAsync();

            var user = await database.Users.SingleAsync(u => u.Email == TestEmail);

            var organizationUser = await database.OrganizationUsers.SingleAsync(
                ou => ou.OrganizationId == organization.Id && ou.UserId == user.Id);

            organizationUser.ResetPasswordKey = "something";

            await database.SaveChangesAsync();
        }, MemberDecryptionType.TrustedDeviceEncryption);

        // Assert
        // If the organization has selected TrustedDeviceEncryption but the user still has their master password
        // they can decrypt with either option
        var root = responseBody.RootElement;
        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);

        var userDecryptionOptions = AssertHelper.AssertJsonProperty(root, "UserDecryptionOptions", JsonValueKind.Object);

        // Expected to look like:
        // "UserDecryptionOptions": {
        //   "Object": "userDecryptionOptions"
        //   "HasMasterPassword": true,
        //   "TrustedDeviceOption": {
        //     "HasAdminApproval": true,
        //     "HasManageResetPasswordPermission": false
        //   }
        // }

        // Should have one item for master password & one for trusted device with admin approval
        AssertHelper.AssertJsonProperty(userDecryptionOptions, "HasMasterPassword", JsonValueKind.True);

        var trustedDeviceOption = AssertHelper.AssertJsonProperty(userDecryptionOptions, "TrustedDeviceOption", JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(trustedDeviceOption, "HasAdminApproval", JsonValueKind.True);
    }

    [Fact]
    public async Task SsoLogin_TrustedDeviceEncryptionAndNoMasterPassword_ReturnsOneOption()
    {
        using var responseBody = await RunSuccessTestAsync(async factory =>
        {
            await UpdateUserAsync(factory, user => user.MasterPassword = null);

        }, MemberDecryptionType.TrustedDeviceEncryption);

        // Assert
        // If the organization has selected TrustedDeviceEncryption but the user still has their master password
        // they can decrypt with either option
        var root = responseBody.RootElement;
        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);
        var userDecryptionOptions = AssertHelper.AssertJsonProperty(root, "UserDecryptionOptions", JsonValueKind.Object);

        // Expected to look like:
        // "UserDecryptionOptions": {
        //   "Object": "userDecryptionOptions"
        //   "HasMasterPassword": false,
        //   "TrustedDeviceOption": {
        //     "HasAdminApproval": true,
        //     "HasLoginApprovingDevice": false,
        //     "HasManageResetPasswordPermission": false
        //   }
        // }

        var trustedDeviceOption = AssertHelper.AssertJsonProperty(userDecryptionOptions, "TrustedDeviceOption", JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(trustedDeviceOption, "HasAdminApproval", JsonValueKind.False);
        AssertHelper.AssertJsonProperty(trustedDeviceOption, "HasManageResetPasswordPermission", JsonValueKind.False);

        // This asserts that device keys are not coming back in the response because this should be a new device.
        // if we ever add new properties that come back from here it is fine to change the expected number of properties
        // but it should still be asserted in some way that keys are not amongst them.
        Assert.Collection(trustedDeviceOption.EnumerateObject(),
            p =>
            {
                Assert.Equal("HasAdminApproval", p.Name);
                Assert.Equal(JsonValueKind.False, p.Value.ValueKind);
            },
            p =>
            {
                Assert.Equal("HasLoginApprovingDevice", p.Name);
                Assert.Equal(JsonValueKind.False, p.Value.ValueKind);
            },
            p =>
            {
                Assert.Equal("HasManageResetPasswordPermission", p.Name);
                Assert.Equal(JsonValueKind.False, p.Value.ValueKind);
            },
            p =>
            {
                Assert.Equal("IsTdeOffboarding", p.Name);
                Assert.Equal(JsonValueKind.False, p.Value.ValueKind);
            });
    }

    /// <summary>
    /// If a user has a device that is able to accept login with device requests, we should return that state
    /// with the user decryption options.
    /// </summary>
    [Fact]
    public async Task SsoLogin_TrustedDeviceEncryptionAndNoMasterPassword_HasLoginApprovingDevice_ReturnsTrue()
    {
        using var responseBody = await RunSuccessTestAsync(async factory =>
        {
            await UpdateUserAsync(factory, user => user.MasterPassword = null);
            var userRepository = factory.Services.GetRequiredService<IUserRepository>();
            var user = await userRepository.GetByEmailAsync(TestEmail);

            Assert.NotNull(user);

            var deviceRepository = factory.Services.GetRequiredService<IDeviceRepository>();
            await deviceRepository.CreateAsync(new Device
            {
                Identifier = "my_other_device",
                Type = DeviceType.Android,
                Name = "Android",
                UserId = user.Id,
            });
        }, MemberDecryptionType.TrustedDeviceEncryption);

        // Assert
        // If the organization has selected TrustedDeviceEncryption but the user still has their master password
        // they can decrypt with either option
        var root = responseBody.RootElement;
        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);
        var userDecryptionOptions = AssertHelper.AssertJsonProperty(root, "UserDecryptionOptions", JsonValueKind.Object);

        // Expected to look like:
        // "UserDecryptionOptions": {
        //   "Object": "userDecryptionOptions"
        //   "HasMasterPassword": false,
        //   "TrustedDeviceOption": {
        //     "HasAdminApproval": true,
        //     "HasLoginApprovingDevice": true,
        //     "HasManageResetPasswordPermission": false
        //     "IsTdeOffboarding": false
        //   }
        // }

        var trustedDeviceOption = AssertHelper.AssertJsonProperty(userDecryptionOptions, "TrustedDeviceOption", JsonValueKind.Object);

        // This asserts that device keys are not coming back in the response because this should be a new device.
        // if we ever add new properties that come back from here it is fine to change the expected number of properties
        // but it should still be asserted in some way that keys are not amongst them.
        Assert.Collection(trustedDeviceOption.EnumerateObject(),
            p =>
            {
                Assert.Equal("HasAdminApproval", p.Name);
                Assert.Equal(JsonValueKind.False, p.Value.ValueKind);
            },
            p =>
            {
                Assert.Equal("HasLoginApprovingDevice", p.Name);
                Assert.Equal(JsonValueKind.True, p.Value.ValueKind);
            },
            p =>
            {
                Assert.Equal("HasManageResetPasswordPermission", p.Name);
                Assert.Equal(JsonValueKind.False, p.Value.ValueKind);
            },
            p =>
            {
                Assert.Equal("IsTdeOffboarding", p.Name);
                Assert.Equal(JsonValueKind.False, p.Value.ValueKind);
            });
    }

    /// <summary>
    /// Story: When a user signs in with SSO on a device they have already signed in with we need to return the keys
    /// back to them for the current device if it has been trusted before.
    /// </summary>
    [Fact]
    public async Task SsoLogin_TrustedDeviceEncryptionAndNoMasterPassword_DeviceAlreadyTrusted_ReturnsOneOption()
    {
        // Arrange
        var challenge = new string('c', 50);
        var (factory, code) = await CreateFactoryAsync(new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption,
        }, challenge);

        await UpdateUserAsync(factory, user => user.MasterPassword = null);

        var deviceRepository = factory.Services.GetRequiredService<IDeviceRepository>();

        var deviceIdentifier = $"test_id_{Guid.NewGuid()}";

        var user = await factory.Services.GetRequiredService<IUserRepository>().GetByEmailAsync(TestEmail);
        Assert.NotNull(user);

        const string expectedPrivateKey = TestEncryptionConstants.AES256_CBC_HMAC_Encstring;
        const string expectedUserKey = TestEncryptionConstants.AES256_CBC_HMAC_Encstring;

        var device = await deviceRepository.CreateAsync(new Device
        {
            Type = DeviceType.FirefoxBrowser,
            Identifier = deviceIdentifier,
            Name = "Thing",
            UserId = user.Id,
            EncryptedPrivateKey = expectedPrivateKey,
            EncryptedPublicKey = TestEncryptionConstants.AES256_CBC_HMAC_Encstring,
            EncryptedUserKey = expectedUserKey,
        });

        // Act
        var context = await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", SsoAuthorizationCodeTestHelpers.ClientId },
            { "deviceType", "10" },
            { "deviceIdentifier", deviceIdentifier },
            { "deviceName", "firefox" },
            { "twoFactorToken", "TEST"},
            { "twoFactorProvider", "5" }, // RememberMe Provider
            { "twoFactorRemember", "0" },
            { "grant_type", "authorization_code" },
            { "code", code },
            { "code_verifier", challenge },
            { "redirect_uri", SsoAuthorizationCodeTestHelpers.RedirectUri }
        }));

        // Assert
        // If the organization has selected TrustedDeviceEncryption but the user still has their master password
        // they can decrypt with either option
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = responseBody.RootElement;
        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);
        var userDecryptionOptions = AssertHelper.AssertJsonProperty(root, "UserDecryptionOptions", JsonValueKind.Object);

        // Expected to look like:
        // "UserDecryptionOptions": {
        //   "Object": "userDecryptionOptions"
        //   "HasMasterPassword": false,
        //   "TrustedDeviceOption": {
        //     "HasAdminApproval": true,
        //     "HasManageResetPasswordPermission": false,
        //     "EncryptedPrivateKey": "2.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==",
        //     "EncryptedUserKey": "2.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA=="
        //   }
        // }

        var trustedDeviceOption = AssertHelper.AssertJsonProperty(userDecryptionOptions, "TrustedDeviceOption", JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(trustedDeviceOption, "HasAdminApproval", JsonValueKind.False);
        AssertHelper.AssertJsonProperty(trustedDeviceOption, "HasManageResetPasswordPermission", JsonValueKind.False);

        var actualPrivateKey = AssertHelper.AssertJsonProperty(trustedDeviceOption, "EncryptedPrivateKey", JsonValueKind.String).GetString();
        Assert.Equal(expectedPrivateKey, actualPrivateKey);
        var actualUserKey = AssertHelper.AssertJsonProperty(trustedDeviceOption, "EncryptedUserKey", JsonValueKind.String).GetString();
        Assert.Equal(expectedUserKey, actualUserKey);
    }

    // we should add a test case for JIT provisioned users. They don't have any orgs which caused
    // an error in the UserHasManageResetPasswordPermission set logic.

    /// <summary>
    /// Story: When a user with TDE and the manage reset password permission signs in with SSO, we should return
    ///  TrustedDeviceEncryption.HasManageResetPasswordPermission as true
    /// </summary>
    [Fact]
    public async Task SsoLogin_TrustedDeviceEncryption_UserHasManageResetPasswordPermission_ReturnsTrue()
    {
        // Arrange
        var challenge = new string('c', 50);

        // create user permissions with the ManageResetPassword permission
        var permissionsWithManageResetPassword = new Permissions() { ManageResetPassword = true };

        var (factory, code) = await CreateFactoryAsync(new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption,
        }, challenge, permissions: permissionsWithManageResetPassword);

        // Act
        var context = await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", SsoAuthorizationCodeTestHelpers.ClientId },
            { "deviceType", "10" },
            { "deviceIdentifier", "test_id" },
            { "deviceName", "firefox" },
            { "twoFactorToken", "TEST"},
            { "twoFactorProvider", "5" }, // RememberMe Provider
            { "twoFactorRemember", "0" },
            { "grant_type", "authorization_code" },
            { "code", code },
            { "code_verifier", challenge },
            { "redirect_uri", SsoAuthorizationCodeTestHelpers.RedirectUri }
        }));

        // Assert
        // If the organization has selected TrustedDeviceEncryption but the user still has their master password
        // they can decrypt with either option
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = responseBody.RootElement;
        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);

        var userDecryptionOptions = AssertHelper.AssertJsonProperty(root, "UserDecryptionOptions", JsonValueKind.Object);

        var trustedDeviceOption = AssertHelper.AssertJsonProperty(userDecryptionOptions, "TrustedDeviceOption", JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(trustedDeviceOption, "HasAdminApproval", JsonValueKind.False);
        AssertHelper.AssertJsonProperty(trustedDeviceOption, "HasManageResetPasswordPermission", JsonValueKind.True);

    }

    [Fact]
    public async Task SsoLogin_TrustedDeviceEncryption_ProviderUserHasManageResetPassword_ReturnsCorrectOptions()
    {
        var challenge = new string('c', 50);

        var (factory, code) = await CreateFactoryAsync(new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption,
        }, challenge);

        var user = await factory.Services.GetRequiredService<IUserRepository>().GetByEmailAsync(TestEmail);
        Assert.NotNull(user);
        var providerRepository = factory.Services.GetRequiredService<IProviderRepository>();
        var provider = await providerRepository.CreateAsync(new Provider
        {
            Name = "Test Provider",
        });

        var providerUserRepository = factory.Services.GetRequiredService<IProviderUserRepository>();
        await providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = user.Id,
            Status = ProviderUserStatusType.Confirmed,
            Permissions = CoreHelpers.ClassToJsonData(new Permissions
            {
                ManageResetPassword = true,
            }),
        });

        var organizationUserRepository = factory.Services.GetRequiredService<IOrganizationUserRepository>();
        var organizationUser = (await organizationUserRepository.GetManyByUserAsync(user.Id)).Single();

        var providerOrganizationRepository = factory.Services.GetRequiredService<IProviderOrganizationRepository>();
        await providerOrganizationRepository.CreateAsync(new ProviderOrganization
        {
            ProviderId = provider.Id,
            OrganizationId = organizationUser.OrganizationId,
        });

        // Act
        var context = await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", SsoAuthorizationCodeTestHelpers.ClientId },
            { "deviceType", "10" },
            { "deviceIdentifier", "test_id" },
            { "deviceName", "firefox" },
            { "twoFactorToken", "TEST"},
            { "twoFactorProvider", "5" }, // RememberMe Provider
            { "twoFactorRemember", "0" },
            { "grant_type", "authorization_code" },
            { "code", code },
            { "code_verifier", challenge },
            { "redirect_uri", SsoAuthorizationCodeTestHelpers.RedirectUri }
        }));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var responseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = responseBody.RootElement;
        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);

        var userDecryptionOptions = AssertHelper.AssertJsonProperty(root, "UserDecryptionOptions", JsonValueKind.Object);

        var trustedDeviceOption = AssertHelper.AssertJsonProperty(userDecryptionOptions, "TrustedDeviceOption", JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(trustedDeviceOption, "HasAdminApproval", JsonValueKind.False);
        AssertHelper.AssertJsonProperty(trustedDeviceOption, "HasManageResetPasswordPermission", JsonValueKind.True);
    }
}
