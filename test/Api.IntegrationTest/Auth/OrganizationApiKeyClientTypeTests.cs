using System.Collections.Concurrent;
using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Identity;
using Bit.Core.Billing.Enums;
using Bit.Core.Context;
using Bit.Core.Enums;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Bit.Api.IntegrationTest.Auth;

public class OrganizationApiKeyClientTypeTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly LoginHelper _loginHelper;
    private readonly ConcurrentQueue<ICurrentContext> _currentContexts = new();

    private Organization _organization = null!;

    public OrganizationApiKeyClientTypeTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.ConfigureServices(services =>
        {
            services.RemoveAll<ICurrentContext>();
            services.AddScoped<ICurrentContext>(sp =>
            {
                var currentContext = ActivatorUtilities.CreateInstance<CurrentContext>(sp);
                _currentContexts.Enqueue(currentContext);
                return currentContext;
            });
        });
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        var ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: ownerEmail, passwordManagerSeats: 10, paymentMethod: PaymentMethodType.Card);

        await _loginHelper.LoginWithOrganizationApiKeyAsync(_organization.Id);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task OrganizationApiKeyToken_ResolvesOrganizationClientType()
    {
        var response = await _client.GetAsync("/public/members");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var currentContext = Assert.Single(_currentContexts,
            c => c.ClientId == $"organization.{_organization.Id}");
        Assert.Equal(IdentityClientType.Organization, currentContext.IdentityClientType);
        Assert.Equal(_organization.Id, currentContext.OrganizationId);
    }
}
