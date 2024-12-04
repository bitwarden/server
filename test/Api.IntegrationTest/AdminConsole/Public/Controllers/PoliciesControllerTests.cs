using System.Net;
using System.Text.Json;
using Bit.Api.AdminConsole.Public.Models.Request;
using Bit.Api.AdminConsole.Public.Models.Response;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Public.Controllers;

public class PoliciesControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    // These will get set in `InitializeAsync` which is ran before all tests
    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public PoliciesControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
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
    public async Task Post_NewPolicy()
    {
        var policyType = PolicyType.MasterPassword;
        var request = new PolicyUpdateRequestModel
        {
            Enabled = true,
            Data = new Dictionary<string, object>
            {
                { "minComplexity", 15},
                { "requireLower", true}
            }
        };

        var response = await _client.PutAsync($"/public/policies/{policyType}", JsonContent.Create(request));

        // Assert against the response
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PolicyResponseModel>();
        Assert.NotNull(result);

        Assert.True(result.Enabled);
        Assert.Equal(policyType, result.Type);
        Assert.IsType<Guid>(result.Id);
        Assert.NotEqual(default, result.Id);
        Assert.NotNull(result.Data);
        Assert.Equal(15, ((JsonElement)result.Data["minComplexity"]).GetInt32());
        Assert.True(((JsonElement)result.Data["requireLower"]).GetBoolean());

        // Assert against the database values
        var policyRepository = _factory.GetService<IPolicyRepository>();
        var policy = await policyRepository.GetByOrganizationIdTypeAsync(_organization.Id, policyType);
        Assert.NotNull(policy);

        Assert.True(policy.Enabled);
        Assert.Equal(policyType, policy.Type);
        Assert.IsType<Guid>(policy.Id);
        Assert.NotEqual(default, policy.Id);
        Assert.Equal(_organization.Id, policy.OrganizationId);

        Assert.NotNull(policy.Data);
        var data = policy.GetDataModel<MasterPasswordPolicyData>();
        var expectedData = new MasterPasswordPolicyData { MinComplexity = 15, RequireLower = true };
        AssertHelper.AssertPropertyEqual(expectedData, data);
    }

    [Fact]
    public async Task Post_UpdatePolicy()
    {
        var policyType = PolicyType.MasterPassword;
        var existingPolicy = new Policy
        {
            OrganizationId = _organization.Id,
            Enabled = true,
            Type = policyType
        };
        existingPolicy.SetDataModel(new MasterPasswordPolicyData
        {
            EnforceOnLogin = true,
            MinLength = 22,
            RequireSpecial = true
        });

        var policyRepository = _factory.GetService<IPolicyRepository>();
        await policyRepository.UpsertAsync(existingPolicy);

        // The Id isn't set until it's created in the database, get it back out to get the id
        var createdPolicy = await policyRepository.GetByOrganizationIdTypeAsync(_organization.Id, policyType);
        var expectedId = createdPolicy!.Id;

        var request = new PolicyUpdateRequestModel
        {
            Enabled = false,
            Data = new Dictionary<string, object>
            {
                { "minLength", 15},
                { "requireUpper", true}
            }
        };

        var response = await _client.PutAsync($"/public/policies/{policyType}", JsonContent.Create(request));

        // Assert against the response
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PolicyResponseModel>();
        Assert.NotNull(result);

        Assert.False(result.Enabled);
        Assert.Equal(policyType, result.Type);
        Assert.Equal(expectedId, result.Id);
        Assert.NotNull(result.Data);
        Assert.Equal(15, ((JsonElement)result.Data["minLength"]).GetInt32());
        Assert.True(((JsonElement)result.Data["requireUpper"]).GetBoolean());

        // Assert against the database values
        var policy = await policyRepository.GetByOrganizationIdTypeAsync(_organization.Id, policyType);
        Assert.NotNull(policy);

        Assert.False(policy.Enabled);
        Assert.Equal(policyType, policy.Type);
        Assert.Equal(expectedId, policy.Id);
        Assert.Equal(_organization.Id, policy.OrganizationId);

        Assert.NotNull(policy.Data);
        var data = policy.GetDataModel<MasterPasswordPolicyData>();
        Assert.Equal(15, data.MinLength);
        Assert.Equal(true, data.RequireUpper);
    }
}
