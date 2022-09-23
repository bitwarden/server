using Bit.Api.IntegrationTest.Factories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api.Request.Accounts;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.Scenarios;

public class PushSyncTests
{
    [Fact]
    public async Task ValidatePushSync_MakesItsWayToCloud()
    {
        var (cloudApiFactory, selfHostedApiFactory) = await SetupAsync(async installationRepo =>
        {
            var installation = await installationRepo.CreateAsync(new Installation
            {
                Key = "MYKEY",
                Enabled = true,
            });

            return (installation.Id.ToString(), installation.Key);
        });

        await CreateCipherAsync(selfHostedApiFactory, "test+pushsync@email.com");

        // Get the substituted services out of the cloud services
        var pushNotificationService = cloudApiFactory.Services.GetRequiredService<IPushNotificationService>();

        // Validate that the call made it to cloud
        await pushNotificationService.Received(1)
            .SendPayloadToUserAsync(
                Arg.Any<string>(),
                PushType.SyncCipherCreate,
                Arg.Any<object>(),
                Arg.Any<string>(),
                Arg.Any<string>());
    }

    [Fact]
    public async Task ValidatePushSync_BadInstallationKey_DoesNotMakeItToCloud()
    {
        var (cloudApiFactory, selfHostedApiFactory) = await SetupAsync(async installationRepo =>
        {
            var installation = await installationRepo.CreateAsync(new Installation
            {
                Key = "REAL_KEY",
                Enabled = true,
            });

            return (installation.Id.ToString(), "BAD_KEY");
        });

        await CreateCipherAsync(selfHostedApiFactory, "test+pushsync-bad-id@email.com");

        // Get the substituted services out of the cloud services
        var pushNotificationService = cloudApiFactory.Services.GetRequiredService<IPushNotificationService>();

        // Validate that the call made it to cloud
        await pushNotificationService.DidNotReceive()
            .SendPayloadToUserAsync(
                Arg.Any<string>(),
                Arg.Any<PushType>(),
                Arg.Any<object>(),
                Arg.Any<string>()
            );
    }

    private static async Task<(ApiApplicationFactory CloudApi, ApiApplicationFactory SelfHostedApi)> SetupAsync(
        Func<IInstallationRepository, Task<(string InstallationId, string InstallationKey)>> installationSetup)
    {
        var cloudApiFactory = new ApiApplicationFactory();

        cloudApiFactory.SubstituteService<IPushNotificationService>();

        var installationRepo = cloudApiFactory.Services.GetRequiredService<IInstallationRepository>();
        // Cloud API has started up here and can NOT be configured below anymore

        var (id, key) = await installationSetup(installationRepo);

        var selfHostedApiFactory = new ApiApplicationFactory
        {
            SelfHosted = true,
            DatabaseName = "SELFHOSTED_DB"
        };

        selfHostedApiFactory.Identity.SelfHosted = true;
        selfHostedApiFactory.Identity.DatabaseName = "SELFHOSTED_DB";

        // Just has to exist and be a valid Uri
        selfHostedApiFactory.AddConfiguration("globalSettings:pushRelayBaseUri", "https://localhost");
        selfHostedApiFactory.AddConfiguration("globalSettings:installation:id", id);
        selfHostedApiFactory.AddConfiguration("globalSettings:installation:key", key);

        // Override the primary http handler for these HttpClients with an in memory one
        selfHostedApiFactory.OverrideHttpHandler(HttpClientNames.CloudIdentityRelayPush, cloudApiFactory.Identity.Server.CreateHandler());
        selfHostedApiFactory.OverrideHttpHandler(HttpClientNames.CloudApiRelayPush, cloudApiFactory.Server.CreateHandler());

        return (cloudApiFactory, selfHostedApiFactory);
    }

    private static async Task CreateCipherAsync(ApiApplicationFactory selfHostedApiFactory, string email)
    {
        // Create a user
        await selfHostedApiFactory.Identity.RegisterAsync(new RegisterRequestModel
        {
            Email = email,
            MasterPasswordHash = "master_password_hash"
        });

        // Sign in user
        var (token, _) = await selfHostedApiFactory.LoginWithNewAccount(email);

        // Setup an HttpClient for making calls as the user

        var selfHostApiClient = selfHostedApiFactory.CreateAuthenticatedClient(token);

        var response = await selfHostApiClient.PostAsJsonAsync("ciphers", new
        {
            type = CipherType.Login,
            name = "2.TXlQYXNzd29yZA==|TXlQYXNzd29yZA==|TXlQYXNzd29yZA==",
            login = new { }
        });

        // No matter what the cipher should have been able to be created
        response.EnsureSuccessStatusCode();

        // PushNotification items aren't awaited and are fire-and-forget so we need a little extra time to let the calls happen
        await Task.Delay(TimeSpan.FromSeconds(1));
    }
}
