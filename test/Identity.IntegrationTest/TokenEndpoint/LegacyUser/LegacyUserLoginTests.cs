using System.Text.Json;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.TokenEndpointTestHelpers;

namespace Bit.Identity.IntegrationTest.TokenEndpoint.LegacyUser;

[SutProviderCustomize]
public class LegacyUserLoginTests
{
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_GrantTypeClientCredentials_AsLegacyUser_Fails(
        RegisterFinishRequestModel model,
        string deviceId)
    {
        model.UserAsymmetricKeys.AccountKeys = null;
        var localFactory = new IdentityApplicationFactory();
        var server = localFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("globalSettings:launchDarkly:flagValues:block-legacy-users", "true");
        }).Server;

        model.Email = "test+tokenclientcredentials@email.com";
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(model);

        // Modify user to be legacy user. We have to fetch the user again to put it in the ef-context
        // so when we modify change tracking will save the changes.
        var database = localFactory.GetDatabaseContext();
        user = await database.Users
            .FirstAsync(u => u.Email == model.Email);
        user.Key = null;
        await database.SaveChangesAsync();

        var context = await server.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                { "scope", "api offline_access" },
                { "client_id", "browser" },
                { "deviceType", DeviceTypeAsString(DeviceType.ChromeBrowser) },
                { "deviceIdentifier", deviceId },
                { "deviceName", "chrome" },
                { "grant_type", "password" },
                { "username", model.Email },
                { "password", model.MasterPasswordHash },
            }));

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        var errorBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var error = AssertHelper.AssertJsonProperty(errorBody.RootElement, "ErrorModel", JsonValueKind.Object);
        var message = AssertHelper.AssertJsonProperty(error, "Message", JsonValueKind.String).GetString();
        Assert.StartsWith("Legacy encryption without a userkey is no longer supported.", message);
    }
}
