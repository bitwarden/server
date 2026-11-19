using System.Net;
using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Options;
using Bit.Seeder.Scenes;
using Bit.Seeder.Services;
using Bit.SeederApi.Models.Request;
using Bit.SeederApi.Models.Response;
using Duende.IdentityModel.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

/// <summary>
/// Drives <see cref="UserStripeBillingScene"/> through POST /seed with the Stripe billing service substituted,
/// so nothing reaches Stripe or the pricing service.
/// </summary>
public class UserStripeBillingSceneTests : IAsyncLifetime
{
    private const string Username = "username";
    private const string Password = "pass";

    private readonly IUserStripeBillingService _billingService = Substitute.For<IUserStripeBillingService>();
    private readonly SeederApiApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly string _playId = Guid.NewGuid().ToString();

    public UserStripeBillingSceneTests()
    {
        _factory = new SeederApiApplicationFactory();
        _factory.ConfigureAuth(Username, Password);
        _factory.ConfigureServices(services =>
        {
            services.RemoveAll<IUserStripeBillingService>();
            services.AddSingleton(_billingService);
        });
        _client = _factory.CreateClient();
        _client.SetBasicAuthentication(Username, Password);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _client.DeleteAsync("/seed");
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task SeedEndpoint_BillsTheExistingUserAndPersistsItsPremiumFields()
    {
        var userId = await SeedUserAsync();
        var seededRevisionDate = (await LoadUserAsync(userId)).RevisionDate;
        var expiration = DateTime.UtcNow.AddYears(1);
        _billingService
            .When(s => s.CreateSubscriptionAsync(
                Arg.Any<User>(), Arg.Any<UserSubscriptionType>(), Arg.Any<StripeBillingProfile>()))
            .Do(call =>
            {
                var user = call.Arg<User>();
                user.Premium = true;
                user.PremiumExpirationDate = expiration;
                user.Gateway = GatewayType.Stripe;
                user.GatewayCustomerId = "cus_x";
                user.GatewaySubscriptionId = "sub_x";
                user.MaxStorageGb = 5;
            });

        var response = await PostBillingAsync(new UserStripeBillingScene.Request { UserId = userId });

        var result = await ReadResultAsync(response);
        Assert.Equal(userId, result.GetProperty("userId").GetGuid());
        Assert.Equal("cus_x", result.GetProperty("gatewayCustomerId").GetString());
        Assert.Equal("sub_x", result.GetProperty("gatewaySubscriptionId").GetString());
        Assert.Equal(expiration, result.GetProperty("premiumExpirationDate").GetDateTime(), TimeSpan.FromSeconds(1));

        var row = await LoadUserAsync(userId);
        Assert.True(row.Premium);
        Assert.Equal(GatewayType.Stripe, row.Gateway);
        Assert.Equal("cus_x", row.GatewayCustomerId);
        Assert.Equal("sub_x", row.GatewaySubscriptionId);
        Assert.Equal((short)5, row.MaxStorageGb);
        Assert.Equal(expiration, row.PremiumExpirationDate!.Value, TimeSpan.FromSeconds(1));
        Assert.True(row.RevisionDate > seededRevisionDate);
    }

    [Fact]
    public async Task SeedEndpoint_UnknownUser_ReturnsBadRequestWithoutBilling()
    {
        var response = await PostBillingAsync(new UserStripeBillingScene.Request { UserId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not found", await response.Content.ReadAsStringAsync());
        await AssertNotBilledAsync();
    }

    [Fact]
    public async Task SeedEndpoint_UserAlreadyLinkedToAGateway_ReturnsBadRequestWithoutBilling()
    {
        var userId = await SeedUserAsync(gatewayCustomerId: "cus_123");

        var response = await PostBillingAsync(new UserStripeBillingScene.Request { UserId = userId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("already linked", await response.Content.ReadAsStringAsync());
        await AssertNotBilledAsync();
    }

    [Fact]
    public async Task SeedEndpoint_TaxIdAddress_ReturnsBadRequestWithoutBilling()
    {
        var userId = await SeedUserAsync();

        var response = await PostBillingAsync(new UserStripeBillingScene.Request
        {
            UserId = userId,
            Address = "de-vat"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("tax ID", await response.Content.ReadAsStringAsync());
        await AssertNotBilledAsync();
    }

    [Fact]
    public async Task SeedEndpoint_StripeFailure_ReturnsTheCustomerToCancel()
    {
        var userId = await SeedUserAsync();
        _billingService
            .CreateSubscriptionAsync(Arg.Any<User>(), Arg.Any<UserSubscriptionType>(), Arg.Any<StripeBillingProfile>())
            .Returns(Task.FromException(new InvalidOperationException(
                "Stripe billing failed. Gateway customer: cus_orphan. Cancel any orphaned Stripe objects.",
                new Exception("Stripe error"))));

        var response = await PostBillingAsync(new UserStripeBillingScene.Request { UserId = userId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("cus_orphan", await response.Content.ReadAsStringAsync());
    }

    private async Task<Guid> SeedUserAsync(string? gatewayCustomerId = null)
    {
        var response = await PostSceneAsync(nameof(SingleUserScene), new SingleUserScene.Request
        {
            Email = $"billing-user-{Guid.NewGuid():N}@example.com",
            Password = "asdfasdfasdf",
            EmailVerified = true,
            Gateway = gatewayCustomerId is null ? null : GatewayType.Stripe,
            GatewayCustomerId = gatewayCustomerId
        });

        return (await ReadResultAsync(response)).GetProperty("userId").GetGuid();
    }

    private async Task<Infrastructure.EntityFramework.Models.User> LoadUserAsync(Guid userId)
    {
        await using var db = _factory.GetDatabaseContext();
        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
    }

    private Task<HttpResponseMessage> PostBillingAsync(UserStripeBillingScene.Request request) =>
        PostSceneAsync(nameof(UserStripeBillingScene), request);

    private Task<HttpResponseMessage> PostSceneAsync<TRequest>(string template, TRequest arguments) =>
        _client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = template,
            Arguments = JsonSerializer.SerializeToElement(arguments)
        }, _playId);

    private static async Task<JsonElement> ReadResultAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var model = await response.Content.ReadFromJsonAsync<SceneResponseModel>();
        Assert.NotNull(model?.Result);
        return (JsonElement)model.Result;
    }

    private Task AssertNotBilledAsync() =>
        _billingService.DidNotReceiveWithAnyArgs().CreateSubscriptionAsync(default!, default, default!);
}
