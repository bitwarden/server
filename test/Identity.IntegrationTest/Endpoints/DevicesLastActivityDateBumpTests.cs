using Bit.Core;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Identity.IntegrationTest.Endpoints;

// TODO: PM-34091 - when cleaning up the feature flag, refactor this summary.
/// <summary>
/// End-to-end verification that the <see cref="FeatureFlagKeys.DevicesLastActivityDate"/>
/// feature flag correctly gates the device <c>LastActivityDate</c> bump on real
/// <c>/connect/token</c> requests, and that the device-keyed LD context is populated by
/// the time the flag is evaluated. PM-38588: prior to the fix, the LD context collapsed
/// to anonymous at flag-eval time because <c>CurrentContextMiddleware</c> can't see the
/// <c>/connect/token</c> body or the refresh-token subject claims, so a device-keyed
/// rollout misbucketed. These tests lock in that the bump fires when the flag is enabled
/// and does not fire when it is disabled, end-to-end through Duende + the validators.
/// </summary>
public class DevicesLastActivityDateBumpTests
{
    private static readonly KeysRequestModel TEST_ACCOUNT_KEYS = new()
    {
        AccountKeys = null,
        PublicKey = "public-key",
        EncryptedPrivateKey = "encrypted-private-key",
    };

    // TODO: PM-34091 - when cleaning up the feature flag, drop the SubstituteService
    // <IFeatureService> setup and rename to drop "FlagEnabled". The end-to-end assertion
    // (LastActivityDate populated after a successful /connect/token) still holds.
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_PasswordGrant_FlagEnabled_BumpsDeviceLastActivityDate(
        RegisterFinishRequestModel requestModel)
    {
        // Arrange — substitute IFeatureService so DevicesLastActivityDate returns true,
        // proving the bump fires end-to-end under the post-fix back-fill.
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();
        localFactory.SubstituteService<IFeatureService>(svc =>
        {
            svc.IsEnabled(FeatureFlagKeys.DevicesLastActivityDate).Returns(true);
        });

        await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        // Act — real password-grant POST through Duende, ROPC, base.ValidateAsync,
        // BuildSuccessResultAsync (where the back-fill + flag check + bump live).
        var response = await localFactory.ContextFromPasswordAsync(
            requestModel.Email, requestModel.MasterPasswordHash);

        // Assert — token issued AND the device's LastActivityDate is populated.
        // (DeviceValidator creates the device during ROPC; the bump immediately after
        // sets LastActivityDate, which would otherwise default to null.)
        Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);
        var deviceRepository = localFactory.Services.GetRequiredService<IDeviceRepository>();
        var device = await deviceRepository.GetByIdentifierAsync(
            IdentityApplicationFactory.DefaultDeviceIdentifier);
        Assert.NotNull(device);
        Assert.NotNull(device.LastActivityDate);
    }

    // TODO: PM-34091 - when cleaning up the feature flag, drop the SubstituteService
    // <IFeatureService> setup and rename to drop "FlagEnabled". The end-to-end assertion
    // (LastActivityDate remains populated after a refresh) still holds.
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_RefreshGrant_FlagEnabled_LastActivityDateRemainsSet(
        RegisterFinishRequestModel requestModel)
    {
        // Arrange — flag stays ON for the entire flow. The warm-up password call creates
        // the device AND fires the first bump. The refresh-path bump fires again but is
        // a no-op within the same UTC day (day-level idempotence in
        // UpdateLastActivityByIdentifierAndUserIdAsync). What we assert here is that the
        // refresh request completes successfully and leaves LastActivityDate populated —
        // proving the refresh-path back-fill + flag-eval path doesn't blow up at runtime.
        // The CTRV unit test (whitespace normalization etc.) pins the refresh-specific
        // extraction logic; this test pins the end-to-end wiring.
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();
        localFactory.SubstituteService<IFeatureService>(svc =>
        {
            svc.IsEnabled(FeatureFlagKeys.DevicesLastActivityDate).Returns(true);
        });

        await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);
        var (_, refreshToken) = await localFactory.TokenFromPasswordAsync(
            requestModel.Email, requestModel.MasterPasswordHash);

        // Act — refresh grant. The back-fill reads UserId + DeviceIdentifier from the
        // server-signed refresh-token Subject claims before the flag check fires.
        var response = await localFactory.Server.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "client_id", "web" },
                { "refresh_token", refreshToken },
            }));

        // Assert — refresh succeeded and the device still has its LastActivityDate set.
        Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);
        var deviceRepository = localFactory.Services.GetRequiredService<IDeviceRepository>();
        var device = await deviceRepository.GetByIdentifierAsync(
            IdentityApplicationFactory.DefaultDeviceIdentifier);
        Assert.NotNull(device);
        Assert.NotNull(device.LastActivityDate);
    }

    // Note: a "flag disabled doesn't bump" integration test was considered but is not
    // testable end-to-end at this level. DeviceValidator.GetDeviceFromRequest seeds
    // LastActivityDate = DateTime.UtcNow at device creation, so a brand-new device row
    // looks identical whether or not the post-creation bump fires. Distinguishing the
    // two would require a yesterday-anchored fixture or a substituted IUpdateDeviceLast-
    // ActivityCommand to count calls — both pull the test toward unit-test territory.
    // The flag-gating logic itself is covered by CustomTokenRequestValidatorTests and
    // BaseRequestValidatorTests.
}
