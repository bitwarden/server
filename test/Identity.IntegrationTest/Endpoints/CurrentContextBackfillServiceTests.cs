using System.Collections.Concurrent;
using System.Security.Claims;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Context;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.Identity.IdentityServer;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Bit.Identity.IntegrationTest.Endpoints;

/// <summary>
/// End-to-end verification that <see cref="ICurrentContextBackfillService"/> is invoked
/// during real <c>/connect/token</c> requests and produces a populated
/// <see cref="ICurrentContext"/> for downstream feature flag evaluation.
///
/// Closes gaps that unit tests can't cover: DI registration, middleware ordering,
/// Duende's pipeline timing for subject parsing, and the grant validators' wiring.
/// </summary>
public class CurrentContextBackfillServiceTests
{
    private static readonly KeysRequestModel TEST_ACCOUNT_KEYS = new()
    {
        AccountKeys = null,
        PublicKey = "public-key",
        EncryptedPrivateKey = "encrypted-private-key",
    };

    private readonly record struct ContextSnapshot(Guid? UserId, string? DeviceIdentifier);

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_GrantTypePassword_BackfillsCurrentContextWithUserAndDevice(
        RegisterFinishRequestModel requestModel)
    {
        // Arrange
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();
        var snapshots = ProbeBackfillService(localFactory);

        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);

        // Act — real password-grant POST through Duende, validators, and back-fill
        var context = await localFactory.ContextFromPasswordAsync(
            user.Email, requestModel.MasterPasswordHash);

        // Assert — request succeeded and CurrentContext was populated for flag eval.
        // For the password grant, Apply runs at least twice:
        //   1. ResourceOwnerPasswordValidator prelude (DeviceIdentifier only — user not yet looked up)
        //   2. base.ValidateAsync (User AND Device from validatorContext)
        // The final snapshot must show both populated.
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.NotEmpty(snapshots);

        var final = snapshots.Last();
        Assert.NotNull(final.UserId);
        Assert.Equal(IdentityApplicationFactory.DefaultDeviceIdentifier, final.DeviceIdentifier);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task TokenEndpoint_GrantTypeRefreshToken_BackfillsCurrentContextFromSubjectClaims(
        RegisterFinishRequestModel requestModel)
    {
        // Arrange — install the probe BEFORE the host is built (first HTTP request).
        // SubstituteService modifies the DI builder, so later substitutions on an
        // already-running host are no-ops.
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();
        var snapshots = ProbeBackfillService(localFactory);

        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);
        var (_, refreshToken) = await localFactory.TokenFromPasswordAsync(
            requestModel.Email, requestModel.MasterPasswordHash);

        // Mark where the warm-up's snapshots end. Anything enqueued after this point
        // belongs to the refresh-token request — no mutation needed.
        var preActSnapshotCount = snapshots.Count;

        // Act — refresh grant. The back-fill source for this path is the refresh-token
        // subject claims (`sub` for UserId, `device` for DeviceIdentifier).
        var context = await localFactory.Server.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "client_id", "web" },
                { "refresh_token", refreshToken },
            }));

        // Assert — refresh succeeded and the subject-path back-fill populated CurrentContext.
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var refreshSnapshots = snapshots.Skip(preActSnapshotCount).ToList();
        Assert.NotEmpty(refreshSnapshots);

        var final = refreshSnapshots.Last();
        Assert.NotNull(final.UserId);
        Assert.Equal(IdentityApplicationFactory.DefaultDeviceIdentifier, final.DeviceIdentifier);
    }

    /// <summary>
    /// Replaces the registered <see cref="ICurrentContextBackfillService"/> with a probe that
    /// (a) forwards to a real instance so production back-fill behavior is exercised, and
    /// (b) snapshots <see cref="ICurrentContext.UserId"/> and <see cref="ICurrentContext.DeviceIdentifier"/>
    /// after each <c>Apply</c> call. Snapshot order matches Apply-call order across all validators in a request.
    /// </summary>
    private static ConcurrentQueue<ContextSnapshot> ProbeBackfillService(IdentityApplicationFactory factory)
    {
        var realService = new CurrentContextBackfillService(
            NullLogger<CurrentContextBackfillService>.Instance);
        var snapshots = new ConcurrentQueue<ContextSnapshot>();

        factory.SubstituteService<ICurrentContextBackfillService>(svc =>
        {
            svc.WhenForAnyArgs(s => s.Apply(default!, default!))
                .Do(call =>
                {
                    var currentContext = call.ArgAt<ICurrentContext>(0);
                    realService.Apply(
                        currentContext,
                        call.ArgAt<ValidatedRequest?>(1),
                        call.ArgAt<ClaimsPrincipal?>(2),
                        call.ArgAt<CustomValidatorRequestContext?>(3));
                    snapshots.Enqueue(new ContextSnapshot(
                        currentContext.UserId,
                        currentContext.DeviceIdentifier));
                });
        });

        return snapshots;
    }
}
