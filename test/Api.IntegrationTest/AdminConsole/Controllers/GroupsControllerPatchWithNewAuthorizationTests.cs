using System.Net;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class GroupsControllerPatchWithNewAuthorizationTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _ownerEmail = null!;
    private Organization _organization = null!;

    public GroupsControllerPatchWithNewAuthorizationTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _factory.SubstituteService<Bit.Core.Services.IFeatureService>(_ => { });
        _factory.SubstituteService<Bitwarden.Server.Sdk.Features.IFeatureService>(featureService =>
            featureService.IsEnabled(FeatureFlagKeys.GroupsAuthorizationServiceEndpoint, Arg.Any<bool>())
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

        // The owner has no direct Manage access on the group's collections, so this is what authorizes
        // them to change the group's collection access.
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
    public async Task PatchWithNewAuthorization_UpdatesGroup_Success()
    {
        var group = await OrganizationTestHelpers.CreateGroup(_factory, _organization.Id);

        var model = new GroupRequestModel
        {
            Name = "renamed-via-new-authorization",
            Collections = [],
            Users = []
        };

        var response = await _client.PatchAsJsonAsync(
            $"organizations/{_organization.Id}/groups/{group.Id}", model);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedGroup = await _factory.GetService<IGroupRepository>().GetByIdAsync(group.Id);
        Assert.NotNull(updatedGroup);
        Assert.Equal("renamed-via-new-authorization", updatedGroup.Name);
    }
}
