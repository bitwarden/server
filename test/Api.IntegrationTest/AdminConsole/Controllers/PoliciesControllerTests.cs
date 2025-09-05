using System.Net;
using System.Text.Json;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using NSubstitute;
using Xunit;

namespace Bit.Api.IntegrationTest.AdminConsole.Controllers;

public class PoliciesControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;

    private Organization _organization = null!;
    private string _ownerEmail = null!;

    public PoliciesControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _factory.SubstituteService<Core.Services.IFeatureService>(featureService =>
        {
            featureService
                .IsEnabled("pm-19467-create-default-location")
                .Returns(true);
        });
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
    }

    public async Task InitializeAsync()
    {
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually2023,
            ownerEmail: _ownerEmail, passwordManagerSeats: 10, paymentMethod: PaymentMethodType.Card);

        await _loginHelper.LoginAsync(_ownerEmail);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PutVNext_OrganizationDataOwnershipPolicy_Success()
    {
        // Arrange
        const PolicyType policyType = PolicyType.OrganizationDataOwnership;
        var request = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Type = policyType,
                Enabled = true,
            },
            Metadata = new Dictionary<string, object>
            {
                { "defaultUserCollectionName", "Test Default Collection" }
            }
        };

        // Act
        var response = await _client.PutAsync($"/organizations/{_organization.Id}/policies/{policyType}/vnext",
            JsonContent.Create(request));

        // Assert
        await AssertResponse();

        await AssertPolicy();
        return;

        async Task AssertResponse()
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var responseContent = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
            Assert.True(result.GetProperty("enabled").GetBoolean());
            Assert.Equal((int)policyType, result.GetProperty("type").GetInt32());
        }

        async Task AssertPolicy()
        {
            var policyRepository = _factory.GetService<IPolicyRepository>();
            var policy = await policyRepository.GetByOrganizationIdTypeAsync(_organization.Id, policyType);
            Assert.NotNull(policy);

            Assert.True(policy.Enabled);
            Assert.Equal(policyType, policy.Type);
            Assert.Null(policy.Data);
            Assert.Equal(_organization.Id, policy.OrganizationId);
        }
    }

    [Fact]
    public async Task PutVNext_MasterPasswordPolicy_Success()
    {
        // Arrange
        var policyType = PolicyType.MasterPassword;
        var request = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Type = policyType,
                Enabled = true,
                Data = new Dictionary<string, object>
                {
                    { "minComplexity", 10 },
                    { "minLength", 12 },
                    { "requireUpper", true },
                    { "requireLower", false },
                    { "requireNumbers", true },
                    { "requireSpecial", false },
                    { "enforceOnLogin", true }
                }
            }
        };

        // Act
        var response = await _client.PutAsync($"/organizations/{_organization.Id}/policies/{policyType}/vnext",
            JsonContent.Create(request));

        // Assert
        await AssertResponse();

        await AssertPolicyDataForMasterPasswordPolicy();
        return;

        async Task AssertPolicyDataForMasterPasswordPolicy()
        {
            var policyRepository = _factory.GetService<IPolicyRepository>();

            var policy = await policyRepository.GetByOrganizationIdTypeAsync(_organization.Id, policyType);
            Assert.NotNull(policy);

            Assert.True(policy.Enabled);
            Assert.Equal(policyType, policy.Type);
            Assert.Equal(_organization.Id, policy.OrganizationId);

            Assert.NotNull(policy.Data);
            var data = policy.GetDataModel<MasterPasswordPolicyData>();
            Assert.Equal(request.Policy.Data["minComplexity"], data.MinComplexity);
            Assert.Equal(request.Policy.Data["minLength"], data.MinLength);
            Assert.Equal(request.Policy.Data["requireUpper"], data.RequireUpper);
            Assert.Equal(request.Policy.Data["requireLower"], data.RequireLower);
            Assert.Equal(request.Policy.Data["requireNumbers"], data.RequireNumbers);
            Assert.Equal(request.Policy.Data["requireSpecial"], data.RequireSpecial);
            Assert.Equal(request.Policy.Data["enforceOnLogin"], data.EnforceOnLogin);
        }

        async Task AssertResponse()
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var responseContent = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
            Assert.True(result.GetProperty("enabled").GetBoolean());
            Assert.Equal((int)policyType, result.GetProperty("type").GetInt32());
        }
    }

}
