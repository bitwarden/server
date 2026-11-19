using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

/// <summary>
/// Endpoints guarded by <c>[Authorize&lt;TRequirement&gt;]</c> must reject unauthenticated and malformed requests with a
/// 4xx response, not a 500.
/// </summary>
public class OrganizationUsersControllerAuthorizationTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _ownerEmail = null!;
    private Guid _organizationId;

    public OrganizationUsersControllerAuthorizationTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"org-users-authz-owner-{Guid.NewGuid()}@example.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 5,
            paymentMethod: PaymentMethodType.Card);
        _organizationId = organization.Id;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("PUT", "users/enable-secrets-manager")]
    [InlineData("PUT", "users/restore")]
    [InlineData("PUT", "users/revoke")]
    [InlineData("GET", "users/mini-details")]
    public async Task Unauthenticated_ReturnsUnauthorized(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), $"organizations/{_organizationId}/{path}");
        if (method == "PUT")
        {
            request.Content = JsonContent.Create(new { ids = new[] { Guid.NewGuid() } });
        }

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("PUT", "users/enable-secrets-manager")]
    [InlineData("PUT", "users/restore")]
    [InlineData("GET", "users/mini-details")]
    public async Task MalformedOrganizationId_ReturnsForbidden(string method, string path)
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var request = new HttpRequestMessage(new HttpMethod(method), $"organizations/notaguid/{path}");
        if (method == "PUT")
        {
            request.Content = JsonContent.Create(new { ids = new[] { Guid.NewGuid() } });
        }

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
