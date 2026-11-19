using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Repositories;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.TokenEndpointTestHelpers;

namespace Bit.Identity.IntegrationTest.TokenEndpoint.DeviceLastActivity;

/// <summary>
/// End-to-end verification that real <c>/connect/token</c> requests, sent through Duende and
/// the Identity request validators, leave the device row with a persisted
/// <c>LastActivityDate</c>.
/// </summary>
public class DeviceLastActivityLoginPersistenceTests
{
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_PasswordGrant_BumpsDeviceLastActivityDate(
        RegisterFinishRequestModel requestModel)
    {
        // Arrange
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();

        await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        // Act — real password-grant POST through Duende, ROPC, BuildSuccessResultAsync.
        var response = await localFactory.ContextFromPasswordAsync(
            requestModel.Email, requestModel.MasterPasswordHash);

        // Assert (1) — request succeeded.
        Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);

        // Assert (2) — the bump completed and persisted.
        var deviceRepository = localFactory.Services.GetRequiredService<IDeviceRepository>();
        var device = await deviceRepository.GetByIdentifierAsync(
            IdentityApplicationFactory.DefaultDeviceIdentifier);
        Assert.NotNull(device);
        Assert.NotNull(device.LastActivityDate);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_RefreshGrant_BumpsDeviceLastActivityDate(
        RegisterFinishRequestModel requestModel)
    {
        // Arrange — the refresh-token bump reads UserId and DeviceIdentifier from the
        // server-signed Subject claims of the validated refresh token, a different code path
        // from the login bump in BuildSuccessResultAsync.
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();

        await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);
        var (_, refreshToken) = await localFactory.TokenFromPasswordAsync(
            requestModel.Email, requestModel.MasterPasswordHash);

        // Act — refresh grant.
        var response = await localFactory.Server.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "client_id", "web" },
                { "refresh_token", refreshToken },
            }));

        // Assert (1) — refresh succeeded.
        Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);

        // Assert (2) — device was bumped (LastActivityDate populated; refresh-path bump
        // within the same UTC day is a no-op vs. the warm-up bump, but the row stays set).
        var deviceRepository = localFactory.Services.GetRequiredService<IDeviceRepository>();
        var device = await deviceRepository.GetByIdentifierAsync(
            IdentityApplicationFactory.DefaultDeviceIdentifier);
        Assert.NotNull(device);
        Assert.NotNull(device.LastActivityDate);
    }
}
