using System.Globalization;
using System.Text.Json;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Test.Auth.AutoFixture;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Identity.IntegrationTest.RequestValidation;

/// <summary>
/// Drives new device verification through a real <c>/connect/token</c> request: an unrecognized device is
/// challenged, a code is emailed, and the code is then redeemed. Asserts that a code is only redeemable by
/// the device that was challenged for it.
/// </summary>
[SutProviderCustomize]
public class NewDeviceVerificationTests
{
    private const string EnableNewDeviceVerificationKey = "globalSettings:enableNewDeviceVerification";

    /// <summary>The device that establishes the account's device history during arrangement.</summary>
    private const string EstablishedDeviceIdentifier = "established-device-identifier";

    /// <summary>The unrecognized device that triggers the challenge and receives the code.</summary>
    private const string ChallengedDeviceIdentifier = "challenged-device-identifier";

    /// <summary>
    /// A second unrecognized device, used to submit a code it never received. Deliberately not
    /// <see cref="EstablishedDeviceIdentifier"/>: a known device short-circuits before the code is ever
    /// examined, which would let the assertion pass without exercising redemption.
    /// </summary>
    private const string OtherDeviceIdentifier = "other-device-identifier";

    private static readonly KeysRequestModel TEST_ACCOUNT_KEYS = new()
    {
        AccountKeys = null,
        PublicKey = "public-key",
        EncryptedPrivateKey = "encrypted-private-key",
    };

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task NewDeviceVerification_UnrecognizedDevice_ChallengesAndSendsCode(
        RegisterFinishRequestModel requestModel)
    {
        var (factory, user) = await ArrangeUserWithDeviceHistoryAsync(requestModel);

        var context = await PostPasswordTokenAsync(
            factory, user.Email, requestModel.MasterPasswordHash, ChallengedDeviceIdentifier);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("new device verification required", await ReadErrorMessageAsync(context));
        Assert.True(factory.TwoFactorEmailCodes.ContainsKey(user.Email));
    }

    // TODO: PM-43465 - Delete this test once every supported client version sends the Device-Identifier
    // header on the new device verification resend request. It covers the record a header-less resend
    // falls back to.
    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task NewDeviceVerification_UnrecognizedDevice_RecordsChallengedDevice(
        RegisterFinishRequestModel requestModel)
    {
        var (factory, user) = await ArrangeUserWithDeviceHistoryAsync(requestModel);

        await PostPasswordTokenAsync(
            factory, user.Email, requestModel.MasterPasswordHash, ChallengedDeviceIdentifier);

        var cache = factory.Services.GetRequiredKeyedService<IFusionCache>(
            NewDeviceVerificationCacheConstants.CacheName);
        var recorded = await cache.GetOrDefaultAsync<string>(user.Id.ToString());

        Assert.Equal(ChallengedDeviceIdentifier, recorded);
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task NewDeviceVerification_CodeSubmittedFromDifferentDevice_IsRejected(
        RegisterFinishRequestModel requestModel)
    {
        var (factory, user) = await ArrangeUserWithDeviceHistoryAsync(requestModel);

        // Challenge the device that will receive the code.
        var challengeContext = await PostPasswordTokenAsync(
            factory, user.Email, requestModel.MasterPasswordHash, ChallengedDeviceIdentifier);
        Assert.Equal("new device verification required", await ReadErrorMessageAsync(challengeContext));

        var code = factory.TwoFactorEmailCodes[user.Email];

        // Submit that code from a device that was never challenged for it.
        var context = await PostPasswordTokenAsync(
            factory, user.Email, requestModel.MasterPasswordHash, OtherDeviceIdentifier, code);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("invalid new device otp", await ReadErrorMessageAsync(context));
    }

    [Theory, BitAutoData, RegisterFinishRequestModelCustomize]
    public async Task NewDeviceVerification_CodeSubmittedFromChallengedDevice_ReturnsAccessToken(
        RegisterFinishRequestModel requestModel)
    {
        var (factory, user) = await ArrangeUserWithDeviceHistoryAsync(requestModel);

        var challengeContext = await PostPasswordTokenAsync(
            factory, user.Email, requestModel.MasterPasswordHash, ChallengedDeviceIdentifier);
        Assert.Equal("new device verification required", await ReadErrorMessageAsync(challengeContext));

        var code = factory.TwoFactorEmailCodes[user.Email];

        var context = await PostPasswordTokenAsync(
            factory, user.Email, requestModel.MasterPasswordHash, ChallengedDeviceIdentifier, code);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        AssertHelper.AssertJsonProperty(body.RootElement, "access_token", JsonValueKind.String);
    }

    /// <summary>
    /// Produces an account that new device verification will actually challenge. Three conditions have to
    /// hold, each of which otherwise causes the flow to pass an unrecognized device straight through:
    /// the setting has to be on, the account has to be older than 24 hours, and the account has to have at
    /// least one device on record.
    /// </summary>
    private static async Task<(IdentityApplicationFactory Factory, User User)> ArrangeUserWithDeviceHistoryAsync(
        RegisterFinishRequestModel requestModel)
    {
        requestModel.UserAsymmetricKeys = TEST_ACCOUNT_KEYS;

        var factory = new IdentityApplicationFactory();
        factory.UpdateConfiguration(EnableNewDeviceVerificationKey, "true");

        var user = await factory.RegisterNewIdentityFactoryUserAsync(requestModel);

        // A brand-new account is passed through, so this login records the account's first device.
        var establishingContext = await PostPasswordTokenAsync(
            factory, user.Email, requestModel.MasterPasswordHash, EstablishedDeviceIdentifier);
        Assert.Equal(StatusCodes.Status200OK, establishingContext.Response.StatusCode);

        // Age the account past the new-account pass-through.
        var database = factory.GetDatabaseContext();
        var databaseUser = await database.Users.SingleAsync(u => u.Id == user.Id);
        databaseUser.CreationDate = DateTime.UtcNow.AddDays(-30);
        await database.SaveChangesAsync();

        return (factory, user);
    }

    private static async Task<HttpContext> PostPasswordTokenAsync(
        IdentityApplicationFactory factory,
        string email,
        string masterPasswordHash,
        string deviceIdentifier,
        string? newDeviceOtp = null)
    {
        var form = new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", "web" },
            { "deviceType", ((int)DeviceType.FirefoxBrowser).ToString(CultureInfo.InvariantCulture) },
            { "deviceIdentifier", deviceIdentifier },
            { "deviceName", "firefox" },
            { "grant_type", "password" },
            { "username", email },
            { "password", masterPasswordHash },
        };

        if (newDeviceOtp != null)
        {
            form.Add("NewDeviceOtp", newDeviceOtp);
        }

        return await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(form));
    }

    private static async Task<string?> ReadErrorMessageAsync(HttpContext context)
    {
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var errorModel = AssertHelper.AssertJsonProperty(body.RootElement, "ErrorModel", JsonValueKind.Object);
        return AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString();
    }
}
