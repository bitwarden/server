using System.Globalization;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Identity.IdentityServer;
using Bit.Identity.IdentityServer.RequestValidators;
using Bit.IntegrationTestCommon.Factories;
using Microsoft.AspNetCore.TestHost;
using NSubstitute;

namespace Bit.Identity.IntegrationTest.Helpers;

/// <summary>
/// Arrange helpers for tests that send requests to the token endpoint.
/// </summary>
public static class TokenEndpointTestHelpers
{
    public static readonly KeysRequestModel TEST_ACCOUNT_KEYS = new KeysRequestModel
    {
        AccountKeys = null,
        PublicKey = "public-key",
        EncryptedPrivateKey = "encrypted-private-key",
    };

    /// <summary>
    /// Prepares a factory that a test class shares across its tests: client version gating accepts every client,
    /// and the users, organizations, organization users, and policies that earlier tests created are removed.
    /// </summary>
    public static void InitializeSharedFactory(IdentityApplicationFactory factory)
    {
        // Bypass client version gating to isolate SSO test behavior
        factory.SubstituteService<IClientVersionValidator>(svc =>
        {
            svc.Validate(Arg.Any<User>(), Arg.Any<CustomValidatorRequestContext>())
                .Returns(true);
        });

        ReinitializeDbForTests(factory);
    }

    public static async Task<HttpContext> PostLoginAsync(
        TestServer server, User user, string MasterPasswordHash)
    {
        return await server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", DeviceTypeAsString(DeviceType.FirefoxBrowser) },
            { "deviceIdentifier", IdentityApplicationFactory.DefaultDeviceIdentifier },
            { "deviceName", "firefox" },
            { "grant_type", "password" },
            { "username", user.Email },
            { "password", MasterPasswordHash },
        }));
    }

    public static string DeviceTypeAsString(DeviceType deviceType)
    {
        return ((int)deviceType).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Removes the users, organizations, organization users, and policies that earlier tests created in a shared
    /// factory.
    /// </summary>
    public static void ReinitializeDbForTests(IdentityApplicationFactory factory)
    {
        var databaseContext = factory.GetDatabaseContext();
        databaseContext.Policies.RemoveRange(databaseContext.Policies);
        databaseContext.OrganizationUsers.RemoveRange(databaseContext.OrganizationUsers);
        databaseContext.Organizations.RemoveRange(databaseContext.Organizations);
        databaseContext.Users.RemoveRange(databaseContext.Users);
        databaseContext.SaveChanges();
    }
}
