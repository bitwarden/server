using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.Pam;

/// <summary>
/// Authorization for <c>organizations/{orgId}/access-rules</c>, exercised over the real request pipeline.
/// </summary>
/// <remarks>
/// The endpoint-registration tests in Pam.Test assert which requirements are attached to which route, but they stop
/// before the pipeline runs — presence in metadata is not enforcement. These tests assert only denials, which stay
/// valid once the handler scaffolds are implemented; the allowed cases assert merely that the caller got past
/// authorization, since the handlers currently throw.
/// </remarks>
public class AccessRuleAuthorizationTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly IFeatureService _featureService;

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public AccessRuleAuthorizationTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IFeatureService>(_ => { });
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _featureService = _factory.GetService<IFeatureService>();
    }

    public async Task InitializeAsync()
    {
        _featureService.IsEnabled(FeatureFlagKeys.Pam).Returns(true);

        _ownerEmail = $"pam-access-rule-authz-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);
        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail, passwordManagerSeats: 10, paymentMethod: PaymentMethodType.Card);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Read_AsNonMember_ReturnsForbidden()
    {
        var outsiderEmail = $"outsider-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(outsiderEmail);
        await _loginHelper.LoginAsync(outsiderEmail);

        var response = await _client.GetAsync(AccessRules());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Write_AsNonMember_ReturnsForbidden(string method)
    {
        var outsiderEmail = $"outsider-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(outsiderEmail);
        await _loginHelper.LoginAsync(outsiderEmail);

        var response = await SendWriteAsync(method);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Write_AsPlainMember_ReturnsForbidden(string method)
    {
        var (memberEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.User);
        await _loginHelper.LoginAsync(memberEmail);

        var response = await SendWriteAsync(method);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Write_AsCustomUserWithoutManageAccessRules_ReturnsForbidden(string method)
    {
        var (customEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Custom, new Permissions { ManageAccessRules = false });
        await _loginHelper.LoginAsync(customEmail);

        var response = await SendWriteAsync(method);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Read_AsProviderUserForTheOrganization_ReturnsForbidden()
    {
        // Providers manage an organization's billing and configuration, but access rules gate who can lease
        // credentials out of it. The group deliberately uses MemberRequirement, not MemberOrProviderRequirement.
        await LoginAsProviderForOrganizationAsync();

        var response = await _client.GetAsync(AccessRules());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Write_AsProviderUserForTheOrganization_ReturnsForbidden()
    {
        await LoginAsProviderForOrganizationAsync();

        var response = await SendWriteAsync("POST");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Read_AsMember_IsNotForbidden()
    {
        // Guards against the group requirement over-denying. The handler is a scaffold that throws, so this
        // asserts only that authorization let the caller through.
        var (memberEmail, _) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.User);
        await _loginHelper.LoginAsync(memberEmail);

        var response = await _client.GetAsync(AccessRules());

        AssertReachedTheHandler(response);
    }

    [Fact]
    public async Task Write_AsOwner_IsNotForbidden()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await SendWriteAsync("POST");

        AssertReachedTheHandler(response);
    }

    /// <summary>
    /// Asserts a caller got past authorization without pinning what the scaffold handler does. NotFound is excluded
    /// as well as Forbidden: without it these would still pass if the PAM feature gate silently swallowed the route,
    /// which would in turn make every denial above pass for the wrong reason. Neither status is a legitimate result
    /// for these two requests once the handlers are implemented.
    /// </summary>
    private static void AssertReachedTheHandler(HttpResponseMessage response)
    {
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    private string AccessRules() => $"organizations/{_organization.Id}/access-rules";

    private Task<HttpResponseMessage> SendWriteAsync(string method) => method switch
    {
        "POST" => _client.PostAsJsonAsync(AccessRules(), new { }),
        "PUT" => _client.PutAsJsonAsync($"{AccessRules()}/{Guid.NewGuid()}", new { }),
        "DELETE" => _client.DeleteAsync($"{AccessRules()}/{Guid.NewGuid()}"),
        _ => throw new ArgumentOutOfRangeException(nameof(method))
    };

    private async Task LoginAsProviderForOrganizationAsync()
    {
        var providerEmail = $"provider-{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(providerEmail);

        await _factory.GetService<ICreateProviderCommand>()
            .CreateBusinessUnitAsync(
                new Provider { Name = "provider", Type = ProviderType.BusinessUnit },
                providerEmail,
                PlanType.EnterpriseAnnually2023,
                10);

        var providerUserAccount = await _factory.GetService<IUserRepository>().GetByEmailAsync(providerEmail);
        var providerUser = (await _factory.GetService<IProviderUserRepository>()
            .GetManyByUserAsync(providerUserAccount!.Id)).First();

        await _factory.GetService<IProviderOrganizationRepository>().CreateAsync(new ProviderOrganization
        {
            ProviderId = providerUser.ProviderId,
            OrganizationId = _organization.Id,
            Key = null,
            Settings = null
        });

        await _loginHelper.LoginAsync(providerEmail);
    }
}
