﻿using System.Security.Claims;
using System.Text.Json;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.Helpers;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using IdentityModel;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

#nullable enable

namespace Bit.Identity.IntegrationTest.Endpoints;

public class IdentityServerSsoTests
{
    const string TestEmail = "sso_user@email.com";

    [Fact]
    public async Task Test_MasterPassword_DecryptionType()
    {
        // Arrange
        using var responseBody = await RunSuccessTestAsync(MemberDecryptionType.MasterPassword);

        // Assert
        // If the organization has a member decryption type of MasterPassword that should be the only option in the reply
        var root = responseBody.RootElement;
        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);
        var userDecryptionOptions = AssertHelper.AssertJsonProperty(root, "UserDecryptionOptions", JsonValueKind.Object);

        // Expected to look like:
        // "UserDecryptionOptions": {
        //   "Object": "userDecryptionOptions"
        //   "HasMasterPassword": true
        // }

        AssertHelper.AssertJsonProperty(userDecryptionOptions, "HasMasterPassword", JsonValueKind.True);

        // One property for the Object and one for master password
        Assert.Equal(2, userDecryptionOptions.EnumerateObject().Count());
    }

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
        var factory = await CreateFactoryAsync(new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption,
        }, challenge);

        await UpdateUserAsync(factory, user => user.MasterPassword = null);

        var deviceRepository = factory.Services.GetRequiredService<IDeviceRepository>();

        var deviceIdentifier = $"test_id_{Guid.NewGuid()}";

        var user = await factory.Services.GetRequiredService<IUserRepository>().GetByEmailAsync(TestEmail);
        Assert.NotNull(user);

        const string expectedPrivateKey = "2.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==";
        const string expectedUserKey = "2.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==";

        var device = await deviceRepository.CreateAsync(new Device
        {
            Type = DeviceType.FirefoxBrowser,
            Identifier = deviceIdentifier,
            Name = "Thing",
            UserId = user.Id,
            EncryptedPrivateKey = expectedPrivateKey,
            EncryptedPublicKey = "2.QmFzZTY0UGFydA==|QmFzZTY0UGFydA==|QmFzZTY0UGFydA==",
            EncryptedUserKey = expectedUserKey,
        });

        // Act
        var context = await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "10" },
            { "deviceIdentifier", deviceIdentifier },
            { "deviceName", "firefox" },
            { "twoFactorToken", "TEST"},
            { "twoFactorProvider", "5" }, // RememberMe Provider
            { "twoFactorRemember", "0" },
            { "grant_type", "authorization_code" },
            { "code", "test_code" },
            { "code_verifier", challenge },
            { "redirect_uri", "https://localhost:8080/sso-connector.html" }
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

        var factory = await CreateFactoryAsync(new SsoConfigurationData
        {
            MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption,
        }, challenge, permissions: permissionsWithManageResetPassword);

        // Act
        var context = await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "10" },
            { "deviceIdentifier", "test_id" },
            { "deviceName", "firefox" },
            { "twoFactorToken", "TEST"},
            { "twoFactorProvider", "5" }, // RememberMe Provider
            { "twoFactorRemember", "0" },
            { "grant_type", "authorization_code" },
            { "code", "test_code" },
            { "code_verifier", challenge },
            { "redirect_uri", "https://localhost:8080/sso-connector.html" }
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

        var factory = await CreateFactoryAsync(new SsoConfigurationData
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
            { "client_id", "web" },
            { "deviceType", "10" },
            { "deviceIdentifier", "test_id" },
            { "deviceName", "firefox" },
            { "twoFactorToken", "TEST"},
            { "twoFactorProvider", "5" }, // RememberMe Provider
            { "twoFactorRemember", "0" },
            { "grant_type", "authorization_code" },
            { "code", "test_code" },
            { "code_verifier", challenge },
            { "redirect_uri", "https://localhost:8080/sso-connector.html" }
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

    [Fact]
    public async Task SsoLogin_KeyConnector_ReturnsOptions()
    {
        using var responseBody = await RunSuccessTestAsync(async factory =>
        {
            await UpdateUserAsync(factory, user => user.MasterPassword = null);
        }, MemberDecryptionType.KeyConnector, "https://key_connector.com");

        var root = responseBody.RootElement;
        AssertHelper.AssertJsonProperty(root, "access_token", JsonValueKind.String);

        var userDecryptionOptions = AssertHelper.AssertJsonProperty(root, "UserDecryptionOptions", JsonValueKind.Object);

        // Expected to look like:
        // "UserDecryptionOptions": {
        //   "Object": "userDecryptionOptions"
        //   "KeyConnectorOption": {
        //     "KeyConnectorUrl": "https://key_connector.com"
        //   }
        // }

        var keyConnectorOption = AssertHelper.AssertJsonProperty(userDecryptionOptions, "KeyConnectorOption", JsonValueKind.Object);

        var keyConnectorUrl = AssertHelper.AssertJsonProperty(keyConnectorOption, "KeyConnectorUrl", JsonValueKind.String).GetString();
        Assert.Equal("https://key_connector.com", keyConnectorUrl);

        // For backwards compatibility reasons the url should also be on the root
        keyConnectorUrl = AssertHelper.AssertJsonProperty(root, "KeyConnectorUrl", JsonValueKind.String).GetString();
        Assert.Equal("https://key_connector.com", keyConnectorUrl);
    }

    private static async Task<JsonDocument> RunSuccessTestAsync(MemberDecryptionType memberDecryptionType)
    {
        return await RunSuccessTestAsync(factory => Task.CompletedTask, memberDecryptionType);
    }

    private static async Task<JsonDocument> RunSuccessTestAsync(Func<IdentityApplicationFactory, Task> configureFactory,
        MemberDecryptionType memberDecryptionType,
        string? keyConnectorUrl = null,
        bool trustedDeviceEnabled = true)
    {
        var challenge = new string('c', 50);
        var factory = await CreateFactoryAsync(new SsoConfigurationData
        {
            MemberDecryptionType = memberDecryptionType,
            KeyConnectorUrl = keyConnectorUrl,
        }, challenge, trustedDeviceEnabled);

        await configureFactory(factory);
        var context = await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", "10" },
            { "deviceIdentifier", "test_id" },
            { "deviceName", "firefox" },
            { "twoFactorToken", "TEST"},
            { "twoFactorProvider", "5" }, // RememberMe Provider
            { "twoFactorRemember", "0" },
            { "grant_type", "authorization_code" },
            { "code", "test_code" },
            { "code_verifier", challenge },
            { "redirect_uri", "https://localhost:8080/sso-connector.html" }
        }));

        // Only calls that result in a 200 OK should call this helper
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        return await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
    }

    private static async Task<IdentityApplicationFactory> CreateFactoryAsync(
        SsoConfigurationData ssoConfigurationData,
        string challenge,
        bool trustedDeviceEnabled = true,
        Permissions? permissions = null)
    {
        var factory = new IdentityApplicationFactory();

        var authorizationCode = new AuthorizationCode
        {
            ClientId = "web",
            CreationTime = DateTime.UtcNow,
            Lifetime = (int)TimeSpan.FromMinutes(5).TotalSeconds,
            RedirectUri = "https://localhost:8080/sso-connector.html",
            RequestedScopes = ["api", "offline_access"],
            CodeChallenge = challenge.Sha256(),
            CodeChallengeMethod = "plain",
            Subject = null!, // Temporarily set it to null
        };

        factory.SubstituteService<IAuthorizationCodeStore>(service =>
        {
            service.GetAuthorizationCodeAsync("test_code")
                .Returns(authorizationCode);
        });

        var user = await factory.RegisterNewIdentityFactoryUserAsync(
            new RegisterFinishRequestModel
            {
                Email = TestEmail,
                MasterPasswordHash = "masterPasswordHash",
                Kdf = KdfType.PBKDF2_SHA256,
                KdfIterations = AuthConstants.PBKDF2_ITERATIONS.Default,
                UserAsymmetricKeys = new KeysRequestModel()
                {
                    PublicKey = "public_key",
                    EncryptedPrivateKey = "private_key"
                },
                UserSymmetricKey = "sym_key",
            });

        var organizationRepository = factory.Services.GetRequiredService<IOrganizationRepository>();
        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "billing-email@example.com",
            Plan = "Enterprise",
            UsePolicies = true,
        });

        var organizationUserRepository = factory.Services.GetRequiredService<IOrganizationUserRepository>();

        var orgUserPermissions =
            (permissions == null) ? null : JsonSerializer.Serialize(permissions, JsonHelpers.CamelCase);

        var organizationUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            Permissions = orgUserPermissions
        });

        var ssoConfigRepository = factory.Services.GetRequiredService<ISsoConfigRepository>();
        await ssoConfigRepository.CreateAsync(new SsoConfig
        {
            OrganizationId = organization.Id,
            Enabled = true,
            Data = JsonSerializer.Serialize(ssoConfigurationData, JsonHelpers.CamelCase),
        });

        var subject = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(JwtClaimTypes.Subject, user.Id.ToString()), // Get real user id
            new Claim(JwtClaimTypes.Name, TestEmail),
            new Claim(JwtClaimTypes.IdentityProvider, "sso"),
            new Claim("organizationId", organization.Id.ToString()),
            new Claim(JwtClaimTypes.SessionId, "SOMETHING"),
            new Claim(JwtClaimTypes.AuthenticationMethod, "external"),
            new Claim(JwtClaimTypes.AuthenticationTime, DateTime.UtcNow.AddMinutes(-1).ToEpochTime().ToString())
        }, "Duende.IdentityServer", JwtClaimTypes.Name, JwtClaimTypes.Role));

        authorizationCode.Subject = subject;

        return factory;
    }

    private static async Task UpdateUserAsync(IdentityApplicationFactory factory, Action<User> changeUser)
    {
        var userRepository = factory.Services.GetRequiredService<IUserRepository>();
        var user = await userRepository.GetByEmailAsync(TestEmail);
        Assert.NotNull(user);
        changeUser(user);

        await userRepository.ReplaceAsync(user);
    }
}
