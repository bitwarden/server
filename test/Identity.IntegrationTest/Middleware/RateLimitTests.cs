using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.TokenEndpointTestHelpers;

namespace Bit.Identity.IntegrationTest.Middleware;

[SutProviderCustomize]
public class RateLimitTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public RateLimitTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
        InitializeSharedFactory(_factory);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_TooQuickInOneSecond_BlockRequest(
        RegisterFinishRequestModel requestModel)
    {
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        const int AmountInOneSecondAllowed = 10;

        // The rule we are testing is 10 requests in 1 second
        requestModel.Email = "test+ratelimiting@email.com";

        var localFactory = new IdentityApplicationFactory();
        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        var database = localFactory.GetDatabaseContext();
        user = await database.Users
            .FirstAsync(u => u.Email == user.Email);

        var tasks = new Task<HttpContext>[AmountInOneSecondAllowed + 1];

        for (var i = 0; i < AmountInOneSecondAllowed + 1; i++)
        {
            // Queue all the amount of calls allowed plus 1
            tasks[i] = MakeRequest();
        }

        var responses = (await Task.WhenAll(tasks)).ToList();
        var blockResponses = responses.Where(c => c.Response.StatusCode == StatusCodes.Status429TooManyRequests);

        Assert.True(blockResponses.Count() > 0);

        Task<HttpContext> MakeRequest()
        {
            return _factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "api offline_access" },
                { "client_id", "web" },
                { "deviceType", DeviceTypeAsString(DeviceType.FirefoxBrowser) },
                { "deviceIdentifier", IdentityApplicationFactory.DefaultDeviceIdentifier },
                { "deviceName", "firefox" },
                { "grant_type", "password" },
                { "username", user.Email},
                { "password", "master_password_hash" },
            }), context => context.SetIp("1.1.1.2"));
        }
    }
}
