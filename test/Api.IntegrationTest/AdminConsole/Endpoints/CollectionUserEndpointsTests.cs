using System.Net;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Request;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Endpoints;

public class CollectionUserEndpointsTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _ownerEmail = null!;
    private Organization _organization = null!;

    public CollectionUserEndpointsTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _factory.SubstituteService<Bit.Core.Services.IFeatureService>(_ => { });
        _factory.SubstituteService<Bitwarden.Server.Sdk.Features.IFeatureService>(featureService =>
            featureService.IsEnabled(FeatureFlagKeys.PM12473CollectionUserAccessEndpoint, Arg.Any<bool>())
                .Returns(true));
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually,
            ownerEmail: _ownerEmail,
            passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        // The owner has no direct Manage access on the collection created below, so this is what
        // authorizes them to change other users' access to it.
        _organization.AllowAdminAccessToAllCollectionItems = true;
        await _factory.GetService<IOrganizationRepository>().UpsertAsync(_organization);

        await _loginHelper.LoginAsync(_ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PatchCollectionUserAccess_AddsAndUpdatesAccess_Success()
    {
        var (_, existingUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);
        var (_, newUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);

        var collection = await OrganizationTestHelpers.CreateCollectionAsync(
            _factory,
            _organization.Id,
            "Collection user access test",
            users: [new CollectionAccessSelection { Id = existingUser.Id, ReadOnly = true }]);

        var model = new CollectionUserAccessDeltaRequestModel
        {
            Add = [new SelectionReadOnlyRequestModel { Id = newUser.Id, ReadOnly = true }],
            Update = [new SelectionReadOnlyRequestModel { Id = existingUser.Id, Manage = true }]
        };

        var response = await _client.PatchAsJsonAsync(
            $"organizations/{_organization.Id}/collections/{collection.Id}/users", model);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var (_, accessDetails) = await _factory.GetService<ICollectionRepository>()
            .GetByIdWithAccessAsync(collection.Id);

        Assert.Equal(2, accessDetails.Users.Count());
        Assert.True(accessDetails.Users.Single(u => u.Id == existingUser.Id).Manage);
        Assert.True(accessDetails.Users.Single(u => u.Id == newUser.Id).ReadOnly);
    }
}
