using System.Collections.Concurrent;
using Bit.Core;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Context;
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
/// End-to-end verification that PM-38588's fix lands correctly through Duende + the
/// validators on real <c>/connect/token</c> requests. Each test substitutes
/// <see cref="IFeatureService"/> with a probe that snapshots <see cref="ICurrentContext"/>
/// at the moment <see cref="FeatureFlagKeys.DevicesLastActivityDate"/> is evaluated, then
/// also asserts the downstream bump fired. The snapshot directly tests the fix: prior to
/// PM-38588, the snapshot would show null/empty values because <c>CurrentContextMiddleware</c>
/// can't see the <c>/connect/token</c> body or refresh-token subject claims — and LD would
/// have bucketed against an anonymous context. Post-fix, the snapshot must show the
/// resolved User / Device populated immediately before the flag check fires.
/// </summary>
public class DevicesLastActivityDateBumpTests
{
    private static readonly KeysRequestModel TEST_ACCOUNT_KEYS = new()
    {
        AccountKeys = null,
        PublicKey = "public-key",
        EncryptedPrivateKey = "encrypted-private-key",
    };

    private readonly record struct ContextSnapshot(Guid? UserId, string? DeviceIdentifier);

    // TODO: PM-34091 - when cleaning up the feature flag, drop the SubstituteService
    // <IFeatureService> setup and the snapshot probe; rename to drop "FlagEnabled". The
    // device LastActivityDate assertion still holds and the snapshot becomes moot once
    // there's no IsEnabled call to probe.
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_PasswordGrant_FlagEnabled_BumpsDeviceLastActivityDate(
        RegisterFinishRequestModel requestModel)
    {
        // Arrange — probe IFeatureService so we can snapshot CurrentContext at the moment
        // LD would have bucketed by device. Returns true so the bump still fires.
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();
        var snapshots = ProbeCurrentContextAtFlagEval(localFactory);

        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        // Act — real password-grant POST through Duende, ROPC, BuildSuccessResultAsync.
        var response = await localFactory.ContextFromPasswordAsync(
            requestModel.Email, requestModel.MasterPasswordHash);

        // Assert (1) — request succeeded.
        Assert.Equal(StatusCodes.Status200OK, response.Response.StatusCode);

        // Assert (2) — the back-fill ran before the flag check fired. The snapshot is the
        // direct end-to-end proof of PM-38588: CurrentContext is populated with the
        // resolved User.Id and Device.Identifier at the moment LD would have bucketed.
        Assert.NotEmpty(snapshots);
        var snapshot = snapshots.Last();
        Assert.Equal(user.Id, snapshot.UserId);
        Assert.Equal(IdentityApplicationFactory.DefaultDeviceIdentifier, snapshot.DeviceIdentifier);

        // Assert (3) — the bump completed and persisted.
        var deviceRepository = localFactory.Services.GetRequiredService<IDeviceRepository>();
        var device = await deviceRepository.GetByIdentifierAsync(
            IdentityApplicationFactory.DefaultDeviceIdentifier);
        Assert.NotNull(device);
        Assert.NotNull(device.LastActivityDate);
    }

    // TODO: PM-34091 - when cleaning up the feature flag, drop the SubstituteService
    // <IFeatureService> setup and the snapshot probe; rename to drop "FlagEnabled". The
    // device LastActivityDate assertion still holds and the snapshot becomes moot once
    // there's no IsEnabled call to probe.
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_RefreshGrant_FlagEnabled_BackfillsCurrentContextAndBumps(
        RegisterFinishRequestModel requestModel)
    {
        // Arrange — probe IFeatureService so we can snapshot CurrentContext at flag-eval time
        // for the refresh-token grant specifically. The back-fill for refresh reads UserId
        // and DeviceIdentifier from the server-signed Subject claims of the validated refresh
        // token — a different code path from the login bump in BuildSuccessResultAsync.
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();
        var snapshots = ProbeCurrentContextAtFlagEval(localFactory);

        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);
        var (_, refreshToken) = await localFactory.TokenFromPasswordAsync(
            requestModel.Email, requestModel.MasterPasswordHash);

        // Mark where the warm-up's snapshots end. Anything enqueued after this point
        // belongs to the refresh-token request.
        var preRefreshSnapshotCount = snapshots.Count;

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

        // Assert (2) — the refresh-path back-fill ran before the flag check fired.
        var refreshSnapshots = snapshots.Skip(preRefreshSnapshotCount).ToList();
        Assert.NotEmpty(refreshSnapshots);
        var snapshot = refreshSnapshots.Last();
        Assert.Equal(user.Id, snapshot.UserId);
        Assert.Equal(IdentityApplicationFactory.DefaultDeviceIdentifier, snapshot.DeviceIdentifier);

        // Assert (3) — device was bumped (LastActivityDate populated; refresh-path bump
        // within the same UTC day is a no-op vs. the warm-up bump, but the row stays set).
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

    // TODO: PM-34091 - when cleaning up the feature flag, delete this helper AND the
    // ContextSnapshot record above. Both exist solely to probe CurrentContext at
    // IsEnabled time; once there's no IsEnabled call in the bump path, the probe pattern
    // has no observable hook and the helper becomes orphan code.
    /// <summary>
    /// Substitutes <see cref="IFeatureService"/> with a probe that snapshots
    /// <see cref="ICurrentContext.UserId"/> and <see cref="ICurrentContext.DeviceIdentifier"/>
    /// at the moment <see cref="FeatureFlagKeys.DevicesLastActivityDate"/> is evaluated, then
    /// returns <c>true</c> so the downstream bump path still executes. The probe pulls the
    /// running request's scoped <see cref="ICurrentContext"/> off <see cref="HttpContext.RequestServices"/>
    /// via <see cref="IHttpContextAccessor"/> — which is the only way to see the state that
    /// the real LD client would have seen if it were resolving the flag value.
    /// </summary>
    private static ConcurrentQueue<ContextSnapshot> ProbeCurrentContextAtFlagEval(
        IdentityApplicationFactory factory)
    {
        var snapshots = new ConcurrentQueue<ContextSnapshot>();
        factory.SubstituteService<IFeatureService>(svc =>
        {
            svc.IsEnabled(FeatureFlagKeys.DevicesLastActivityDate)
                .Returns(_ =>
                {
                    var accessor = factory.Services.GetRequiredService<IHttpContextAccessor>();
                    var currentContext = accessor.HttpContext?.RequestServices
                        .GetRequiredService<ICurrentContext>();
                    snapshots.Enqueue(new ContextSnapshot(
                        currentContext?.UserId,
                        currentContext?.DeviceIdentifier));
                    return true;
                });
        });
        return snapshots;
    }
}
