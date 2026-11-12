using Bit.Core;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.UserFeatures.Devices.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.TokenEndpointTestHelpers;

namespace Bit.Identity.IntegrationTest.TokenEndpoint.DeviceLastActivity;

/// <summary>
/// Asserts that the Identity request validators invoke <see cref="IUpdateDeviceLastActivityCommand"/>
/// (or skip it under the feature flag) when a real /connect/token request flows through the pipeline.
/// </summary>
[SutProviderCustomize]
public class DeviceLastActivityLoginTriggerTests
{
    private const string FlagSettingKey =
        $"globalSettings:launchDarkly:flagValues:{FeatureFlagKeys.DevicesLastActivityDate}";

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task PasswordGrant_FlagOn_InvokesUpdateDeviceLastActivityCommand(
        RegisterFinishRequestModel requestModel)
    {
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();
        localFactory.SubstituteService<IUpdateDeviceLastActivityCommand>(_ => { });
        localFactory.UpdateConfiguration(FlagSettingKey, "true");

        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);
        var context = await PostLoginAsync(localFactory.Server, user, requestModel.MasterPasswordHash);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var command = localFactory.Services.GetRequiredService<IUpdateDeviceLastActivityCommand>();
        await command.Received(1).UpdateAsync(
            Arg.Is<Device>(d => d.UserId == user.Id && d.Identifier == IdentityApplicationFactory.DefaultDeviceIdentifier),
            Arg.Any<string?>());
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task PasswordGrant_FlagOff_DoesNotInvokeUpdateDeviceLastActivityCommand(
        RegisterFinishRequestModel requestModel)
    {
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();
        localFactory.SubstituteService<IUpdateDeviceLastActivityCommand>(_ => { });
        localFactory.UpdateConfiguration(FlagSettingKey, "false");

        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);
        var context = await PostLoginAsync(localFactory.Server, user, requestModel.MasterPasswordHash);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        var command = localFactory.Services.GetRequiredService<IUpdateDeviceLastActivityCommand>();
        await command.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        await command.DidNotReceiveWithAnyArgs().UpdateByIdentifierAndUserIdAsync(default!, default, default);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task RefreshTokenGrant_FlagOn_InvokesUpdateByIdentifierAndUserIdAsync(
        RegisterFinishRequestModel requestModel)
    {
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;
        var localFactory = new IdentityApplicationFactory();
        localFactory.SubstituteService<IUpdateDeviceLastActivityCommand>(_ => { });
        localFactory.UpdateConfiguration(FlagSettingKey, "true");

        var user = await localFactory.RegisterNewIdentityFactoryUserAsync(requestModel);
        var (_, refreshToken) = await localFactory.TokenFromPasswordAsync(
            requestModel.Email, requestModel.MasterPasswordHash);

        var command = localFactory.Services.GetRequiredService<IUpdateDeviceLastActivityCommand>();
        command.ClearReceivedCalls();

        var context = await localFactory.Server.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "client_id", "web" },
                { "refresh_token", refreshToken },
            }));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);

        await command.Received(1).UpdateByIdentifierAndUserIdAsync(
            IdentityApplicationFactory.DefaultDeviceIdentifier,
            user.Id,
            Arg.Any<string?>());
    }
}
