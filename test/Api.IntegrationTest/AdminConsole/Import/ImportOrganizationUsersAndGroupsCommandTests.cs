using Bit.Api.AdminConsole.Public.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Xunit;

public class ImportOrganizationUsersAndGroupsCommandTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public ImportOrganizationUsersAndGroupsCommandTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.UpdateConfiguration("globalSettings:launchDarkly:flagValues:pm-22583-refactor-import-async",
            "true");
        _client = _factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        // Create the owner account
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        // Create the organization
        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually2023,
            ownerEmail: _ownerEmail, passwordManagerSeats: 10, paymentMethod: PaymentMethodType.Card);

        // Authorize with the organization api key
        await _loginHelper.LoginWithOrganizationApiKeyAsync(_organization.Id);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Import_Existing_User_Success()
    {
        var (email, orgUser) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory, _organization.Id,
            OrganizationUserType.User);

        var request = new OrganizationImportRequestModel();
        request.LargeImport = false;
        request.OverwriteExisting = false;
        request.Groups = [];
        request.Members = [
            new OrganizationImportRequestModel.OrganizationImportMemberRequestModel
            {
                Email = email,
                ExternalId = Guid.NewGuid().ToString(),
                Deleted = false
            }
        ];

        var response = await _client.PostAsync($"/public/organization/import", JsonContent.Create(request));
        var result = await response.Content.ReadAsStringAsync();

        Assert.Equal("success", result);
        //Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    }
}
