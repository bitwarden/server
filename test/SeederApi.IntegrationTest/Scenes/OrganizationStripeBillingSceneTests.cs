using System.Net;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Seeder.Data.Enums;
using Bit.Seeder.Options;
using Bit.Seeder.Scenes;
using Bit.Seeder.Services;
using Bit.SeederApi.Models.Request;
using Bit.SeederApi.Models.Response;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

/// <summary>
/// Drives <see cref="OrganizationStripeBillingScene"/> through POST /seed with the Stripe billing service
/// substituted, so nothing reaches Stripe or the pricing service.
/// </summary>
public class OrganizationStripeBillingSceneTests : IAsyncLifetime
{
    private const string Username = "username";
    private const string Password = "pass";

    private readonly IOrganizationStripeBillingService _billingService =
        Substitute.For<IOrganizationStripeBillingService>();
    private readonly SeederApiApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly string _playId = Guid.NewGuid().ToString();

    public OrganizationStripeBillingSceneTests()
    {
        _factory = new SeederApiApplicationFactory();
        _factory.ConfigureAuth(Username, Password);
        _factory.ConfigureServices(services =>
        {
            services.RemoveAll<IOrganizationStripeBillingService>();
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
    public async Task SeedEndpoint_BillsTheExistingOrganizationWithTheRequestedOptions()
    {
        var organizationId = await SeedOrganizationAsync();
        _billingService
            .When(s => s.CreateSubscriptionAsync(Arg.Any<Organization>(), Arg.Any<StripeBillingOptions>()))
            .Do(call =>
            {
                var organization = call.Arg<Organization>();
                organization.GatewayCustomerId = "cus_x";
                organization.GatewaySubscriptionId = "sub_x";
            });

        var response = await PostBillingAsync(new OrganizationStripeBillingScene.Request
        {
            OrganizationId = organizationId,
            TrialDays = 7,
            Card = "mastercard",
            Address = "de-vat"
        });

        response.EnsureSuccessStatusCode();
        await _billingService.Received(1).CreateSubscriptionAsync(
            Arg.Is<Organization>(o => o.Id == organizationId),
            new StripeBillingOptions
            {
                SkipTrial = false,
                TrialDays = 7,
                Profile = new StripeBillingProfile
                {
                    Card = StripeTestCard.Mastercard,
                    Address = StripeTestAddress.GermanyWithVatId
                }
            });

        var result = await ReadResultAsync(response);
        Assert.Equal(organizationId, result.GetProperty("organizationId").GetGuid());
        Assert.Equal("cus_x", result.GetProperty("gatewayCustomerId").GetString());
        Assert.Equal("sub_x", result.GetProperty("gatewaySubscriptionId").GetString());
    }

    [Fact]
    public async Task SeedEndpoint_UnknownOrganization_ReturnsBadRequestWithoutBilling()
    {
        var response = await PostBillingAsync(new OrganizationStripeBillingScene.Request
        {
            OrganizationId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not found", await response.Content.ReadAsStringAsync());
        await AssertNotBilledAsync();
    }

    [Fact]
    public async Task SeedEndpoint_OrganizationAlreadyLinkedToAGateway_ReturnsBadRequestWithoutBilling()
    {
        var organizationId = await SeedOrganizationAsync(gatewayCustomerId: "cus_123");

        var response = await PostBillingAsync(new OrganizationStripeBillingScene.Request
        {
            OrganizationId = organizationId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("already linked", await response.Content.ReadAsStringAsync());
        await AssertNotBilledAsync();
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 31)]
    [InlineData(true, 7)]
    public async Task SeedEndpoint_InvalidTrial_ReturnsBadRequestWithoutBilling(bool skipTrial, int trialDays)
    {
        var organizationId = await SeedOrganizationAsync();

        var response = await PostBillingAsync(new OrganizationStripeBillingScene.Request
        {
            OrganizationId = organizationId,
            SkipTrial = skipTrial,
            TrialDays = trialDays
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("TrialDays", await response.Content.ReadAsStringAsync());
        await AssertNotBilledAsync();
    }

    private async Task<Guid> SeedOrganizationAsync(string? gatewayCustomerId = null)
    {
        var user = await ReadResultAsync(await PostSceneAsync(nameof(SingleUserScene), new SingleUserScene.Request
        {
            Email = $"billing-owner-{Guid.NewGuid():N}@example.com",
            Password = "asdfasdfasdf",
            EmailVerified = true
        }));

        var organization = await ReadResultAsync(await PostSceneAsync(nameof(SingleOrganizationScene),
            new SingleOrganizationScene.Request
            {
                OwnerUserId = user.GetProperty("userId").GetGuid(),
                PlanType = PlanType.TeamsAnnually,
                Name = "Billing Org",
                Domain = $"billing-{Guid.NewGuid():N}.example",
                Seats = 5,
                Gateway = gatewayCustomerId is null ? null : GatewayType.Stripe,
                GatewayCustomerId = gatewayCustomerId
            }));

        return organization.GetProperty("organizationId").GetGuid();
    }

    private Task<HttpResponseMessage> PostBillingAsync(OrganizationStripeBillingScene.Request request) =>
        PostSceneAsync(nameof(OrganizationStripeBillingScene), request);

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
        _billingService.DidNotReceiveWithAnyArgs().CreateSubscriptionAsync(default!, default!);
}
