using System.Net;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class OrganizationUsersControllerPatchWithNewAuthorizationTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private string _ownerEmail = null!;
    private Organization _organization = null!;

    public OrganizationUsersControllerPatchWithNewAuthorizationTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<IPushNotificationService>(_ => { });
        _factory.SubstituteService<Bit.Core.Services.IFeatureService>(_ => { });
        _factory.SubstituteService<Bitwarden.Server.Sdk.Features.IFeatureService>(featureService =>
        {
            featureService.IsEnabled(FeatureFlagKeys.OrganizationUserAuthorizationServiceEndpoint, Arg.Any<bool>())
                .Returns(true);
            featureService.IsEnabled(FeatureFlagKeys.ChangeMemberEmailNoMp, Arg.Any<bool>())
                .Returns(false);
        });
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

        // The owner has no direct Manage access on the target's collections, so this is what authorizes
        // them to change the target's collection access.
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
    public async Task PatchWithNewAuthorization_UpdatesOrganizationUser_Success()
    {
        var (_, targetUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(
            _factory, _organization.Id, OrganizationUserType.User);

        var model = new OrganizationUserUpdateRequestModel
        {
            Type = OrganizationUserType.Admin,
            AccessSecretsManager = false,
            AccessPam = false,
            Permissions = new Permissions(),
            Collections = [],
            Groups = []
        };

        var response = await _client.PatchAsJsonAsync(
            $"organizations/{_organization.Id}/users/{targetUser.Id}", model);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updatedUser = await _factory.GetService<IOrganizationUserRepository>().GetByIdAsync(targetUser.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(OrganizationUserType.Admin, updatedUser.Type);
    }
}
