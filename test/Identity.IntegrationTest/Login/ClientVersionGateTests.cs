using System.Text.Json;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.KeyManagement.Enums;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.Identity.IntegrationTest.Login;

public class ClientVersionGateTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public ClientVersionGateTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
        ReinitializeDbForTests(_factory);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_GrantTypePassword_V2User_OnOldClientVersion_Blocked(RegisterFinishRequestModel requestModel)
    {
        var localFactory = new IdentityApplicationFactory
        {
            UseMockClientVersionValidator = false
        };
        var server = localFactory.Server;
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        // Make user V2: set private key to COSE and add signature key pair
        var db = localFactory.GetDatabaseContext();
        var efUser = await db.Users.FirstAsync(u => u.Email == user.Email);
        efUser.PrivateKey = TestEncryptionConstants.V2PrivateKey;
        db.UserSignatureKeyPairs.Add(new Bit.Infrastructure.EntityFramework.Models.UserSignatureKeyPair
        {
            Id = Core.Utilities.CoreHelpers.GenerateComb(),
            UserId = efUser.Id,
            SignatureAlgorithm = SignatureAlgorithm.Ed25519,
            SigningKey = TestEncryptionConstants.V2WrappedSigningKey,
            VerifyingKey = TestEncryptionConstants.V2VerifyingKey,
        });
        await db.SaveChangesAsync();

        var context = await server.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "api offline_access" },
                { "client_id", "web" },
                { "deviceType", "2" },
                { "deviceIdentifier", IdentityApplicationFactory.DefaultDeviceIdentifier },
                { "deviceName", "firefox" },
                { "grant_type", "password" },
                { "username", user.Email },
                { "password", requestModel.MasterPasswordHash },
            }),
            http =>
            {
                http.Request.Headers.Append("Bitwarden-Client-Version", "2025.10.0");
            });

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        var errorBody = await Bit.Test.Common.Helpers.AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var error = Bit.Test.Common.Helpers.AssertHelper.AssertJsonProperty(errorBody.RootElement, "ErrorModel", JsonValueKind.Object);
        var message = Bit.Test.Common.Helpers.AssertHelper.AssertJsonProperty(error, "Message", JsonValueKind.String).GetString();
        Assert.Equal("Please update your app to continue using Bitwarden", message);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_GrantTypePassword_V2User_OnMinClientVersion_Succeeds(RegisterFinishRequestModel requestModel)
    {
        var localFactory = new IdentityApplicationFactory
        {
            UseMockClientVersionValidator = false
        };
        var server = localFactory.Server;
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        // Make user V2
        var db = localFactory.GetDatabaseContext();
        var efUser = await db.Users.FirstAsync(u => u.Email == user.Email);
        efUser.PrivateKey = TestEncryptionConstants.V2PrivateKey;
        db.UserSignatureKeyPairs.Add(new Bit.Infrastructure.EntityFramework.Models.UserSignatureKeyPair
        {
            Id = Core.Utilities.CoreHelpers.GenerateComb(),
            UserId = efUser.Id,
            SignatureAlgorithm = SignatureAlgorithm.Ed25519,
            SigningKey = TestEncryptionConstants.V2WrappedSigningKey,
            VerifyingKey = TestEncryptionConstants.V2VerifyingKey,
        });
        await db.SaveChangesAsync();

        var context = await server.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "api offline_access" },
                { "client_id", "web" },
                { "deviceType", "2" },
                { "deviceIdentifier", IdentityApplicationFactory.DefaultDeviceIdentifier },
                { "deviceName", "firefox" },
                { "grant_type", "password" },
                { "username", user.Email },
                { "password", requestModel.MasterPasswordHash },
            }),
            http =>
            {
                http.Request.Headers.Append("Bitwarden-Client-Version", "2025.11.0");
            });

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private void ReinitializeDbForTests(IdentityApplicationFactory factory)
    {
        var databaseContext = factory.GetDatabaseContext();
        databaseContext.Policies.RemoveRange(databaseContext.Policies);
        databaseContext.OrganizationUsers.RemoveRange(databaseContext.OrganizationUsers);
        databaseContext.Organizations.RemoveRange(databaseContext.Organizations);
        databaseContext.Users.RemoveRange(databaseContext.Users);
        databaseContext.SaveChanges();
    }
}
