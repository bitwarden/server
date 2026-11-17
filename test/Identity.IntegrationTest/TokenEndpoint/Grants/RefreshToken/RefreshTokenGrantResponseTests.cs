using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.TokenEndpointTestHelpers;
using static Bit.Identity.IntegrationTest.Helpers.TokenResponseAssertions;

#nullable enable

namespace Bit.Identity.IntegrationTest.TokenEndpoint.Grants.RefreshToken;

/// <summary>
/// Integration tests for a successful <c>refresh_token</c> grant.
/// </summary>
public class RefreshTokenGrantResponseTests
{
    /// <summary>
    /// A valid refresh token gets a new access token in the default token response.
    /// </summary>
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task RefreshToken_Success(RegisterFinishRequestModel requestModel)
    {
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();

        await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        var (_, refreshToken) = await localFactory.TokenFromPasswordAsync(
            requestModel.Email, requestModel.MasterPasswordHash!);

        using var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "client_id", "web" },
            { "refresh_token", refreshToken },
        });
        var context = await localFactory.Server.PostAsync("/connect/token", formContent);

        using var body = await AssertDefaultTokenBodyAsync(context);
        AssertRefreshTokenExists(body.RootElement);
    }
}
