using Bit.Api.AdminConsole.Public.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Public.Request;
using Bit.Api.Models.Public.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Xunit;

namespace Bit.Api.IntegrationTest.Controllers.Public;

public class CollectionsControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _ownerEmail = null!;
    private Organization _organization = null!;

    public CollectionsControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _factory.SubstituteService<IFeatureService>(_ => { });
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

        await _loginHelper.LoginWithOrganizationApiKeyAsync(_organization.Id);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateCollectionWithMultipleUsersAndVariedPermissions_Success()
    {
        // Arrange
        _organization.AllowAdminAccessToAllCollectionItems = true;
        await _factory.GetService<IOrganizationRepository>().UpsertAsync(_organization);

        var groupRepository = _factory.GetService<IGroupRepository>();
        var group = await groupRepository.CreateAsync(new Group
        {
            OrganizationId = _organization.Id,
            Name = "CollectionControllerTests.CreateCollectionWithMultipleUsersAndVariedPermissions_Success",
            ExternalId = $"CollectionControllerTests.CreateCollectionWithMultipleUsersAndVariedPermissions_Success{Guid.NewGuid()}",
        });

        var (_, user) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory,
            _organization.Id,
            OrganizationUserType.User);

        var collection = await OrganizationTestHelpers.CreateCollectionAsync(
            _factory,
            _organization.Id,
            "Shared Collection with a group",
            externalId: "shared-collection-with-group",
            groups:
            [
                new CollectionAccessSelection { Id = group.Id, ReadOnly = false, HidePasswords = false, Manage = true }
            ],
            users:
            [
                new CollectionAccessSelection { Id = user.Id, ReadOnly = false, HidePasswords = false, Manage = true }
            ]);

        var getCollectionsResponse = await _client.GetFromJsonAsync<ListResponseModel<CollectionResponseModel>>("public/collections");
        var getCollectionResponse = await _client.GetFromJsonAsync<CollectionResponseModel>($"public/collections/{collection.Id}");

        var firstCollection = getCollectionsResponse.Data.First(x => x.ExternalId == "shared-collection-with-group");

        var update = new CollectionUpdateRequestModel
        {
            ExternalId = firstCollection.ExternalId,
            Groups = firstCollection.Groups?.Select(x => new AssociationWithPermissionsRequestModel
            {
                Id = x.Id,
                ReadOnly = x.ReadOnly,
                HidePasswords = x.HidePasswords,
                Manage = x.Manage
            }),
        };

        await _client.PutAsJsonAsync($"public/collections/{firstCollection.Id}", update);

        var result = await _factory.GetService<ICollectionRepository>()
            .GetByIdWithAccessAsync(firstCollection.Id);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Item2.Groups);
        Assert.NotEmpty(result.Item2.Users);
    }
}
