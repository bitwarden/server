using System.Net;
using System.Text.Json;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Test.Common.Helpers;
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

        (_organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory, plan: PlanType.EnterpriseAnnually,
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

        const string defaultCollectionName = "Test Default Collection";
        var request = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Type = policyType,
                Enabled = true,
            },
            Metadata = new Dictionary<string, object>
            {
                { "defaultUserCollectionName", defaultCollectionName }
            }
        };

        var (_, admin) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.Admin);

        var (_, user) = await OrganizationTestHelpers.CreateNewUserWithAccountAsync(_factory,
            _organization.Id, OrganizationUserType.User);

        // Act
        var response = await _client.PutAsync($"/organizations/{_organization.Id}/policies/{policyType}/vnext",
            JsonContent.Create(request));

        // Assert
        await AssertResponse();

        await AssertPolicy();

        await AssertDefaultCollectionCreatedOnlyForUserTypeAsync();
        return;

        async Task AssertDefaultCollectionCreatedOnlyForUserTypeAsync()
        {
            var collectionRepository = _factory.GetService<ICollectionRepository>();
            await AssertUserExpectations(collectionRepository);
            await AssertAdminExpectations(collectionRepository);
        }

        async Task AssertUserExpectations(ICollectionRepository collectionRepository)
        {
            var collections = await collectionRepository.GetManyByUserIdAsync(user.UserId!.Value);
            var defaultCollection = collections.FirstOrDefault(c => c.Name == defaultCollectionName);
            Assert.NotNull(defaultCollection);
            Assert.Equal(_organization.Id, defaultCollection.OrganizationId);
        }

        async Task AssertAdminExpectations(ICollectionRepository collectionRepository)
        {
            var collections = await collectionRepository.GetManyByUserIdAsync(admin.UserId!.Value);
            var defaultCollection = collections.FirstOrDefault(c => c.Name == defaultCollectionName);
            Assert.Null(defaultCollection);
        }

        async Task AssertResponse()
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<PolicyResponseModel>();

            Assert.True(content.Enabled);
            Assert.Equal(policyType, content.Type);
            Assert.Equal(_organization.Id, content.OrganizationId);
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

            AssertPolicy(policy);
            AssertMasterPasswordPolicyData(policy);
        }

        async Task AssertResponse()
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<PolicyResponseModel>();

            Assert.True(content.Enabled);
            Assert.Equal(policyType, content.Type);
            Assert.Equal(_organization.Id, content.OrganizationId);
        }

        void AssertPolicy(Policy policy)
        {
            Assert.NotNull(policy);
            Assert.True(policy.Enabled);
            Assert.Equal(policyType, policy.Type);
            Assert.Equal(_organization.Id, policy.OrganizationId);
            Assert.NotNull(policy.Data);
        }

        void AssertMasterPasswordPolicyData(Policy policy)
        {
            var resultData = policy.GetDataModel<MasterPasswordPolicyData>();

            var json = JsonSerializer.Serialize(request.Policy.Data);
            var expectedData = JsonSerializer.Deserialize<MasterPasswordPolicyData>(json);
            AssertHelper.AssertPropertyEqual(resultData, expectedData);
        }
    }

    [Fact]
    public async Task Put_MasterPasswordPolicy_InvalidDataType_ReturnsBadRequest()
    {
        // Arrange
        var policyType = PolicyType.MasterPassword;
        var request = new PolicyRequestModel
        {
            Type = policyType,
            Enabled = true,
            Data = new Dictionary<string, object>
            {
                { "minLength", "not a number" }, // Wrong type - should be int
                { "requireUpper", true }
            }
        };

        // Act
        var response = await _client.PutAsync($"/organizations/{_organization.Id}/policies/{policyType}",
            JsonContent.Create(request));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("minLength", content); // Verify field name is in error message
    }

    [Fact]
    public async Task Put_SendOptionsPolicy_InvalidDataType_ReturnsBadRequest()
    {
        // Arrange
        var policyType = PolicyType.SendOptions;
        var request = new PolicyRequestModel
        {
            Type = policyType,
            Enabled = true,
            Data = new Dictionary<string, object>
            {
                { "disableHideEmail", "not a boolean" } // Wrong type - should be bool
            }
        };

        // Act
        var response = await _client.PutAsync($"/organizations/{_organization.Id}/policies/{policyType}",
            JsonContent.Create(request));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_ResetPasswordPolicy_InvalidDataType_ReturnsBadRequest()
    {
        // Arrange
        var policyType = PolicyType.ResetPassword;
        var request = new PolicyRequestModel
        {
            Type = policyType,
            Enabled = true,
            Data = new Dictionary<string, object>
            {
                { "autoEnrollEnabled", 123 } // Wrong type - should be bool
            }
        };

        // Act
        var response = await _client.PutAsync($"/organizations/{_organization.Id}/policies/{policyType}",
            JsonContent.Create(request));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutVNext_MasterPasswordPolicy_InvalidDataType_ReturnsBadRequest()
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
                    { "minComplexity", "not a number" }, // Wrong type - should be int
                    { "minLength", 12 }
                }
            }
        };

        // Act
        var response = await _client.PutAsync($"/organizations/{_organization.Id}/policies/{policyType}/vnext",
            JsonContent.Create(request));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("minComplexity", content); // Verify field name is in error message
    }

    [Fact]
    public async Task PutVNext_SendOptionsPolicy_InvalidDataType_ReturnsBadRequest()
    {
        // Arrange
        var policyType = PolicyType.SendOptions;
        var request = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Type = policyType,
                Enabled = true,
                Data = new Dictionary<string, object>
                {
                    { "disableHideEmail", "not a boolean" } // Wrong type - should be bool
                }
            }
        };

        // Act
        var response = await _client.PutAsync($"/organizations/{_organization.Id}/policies/{policyType}/vnext",
            JsonContent.Create(request));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutVNext_ResetPasswordPolicy_InvalidDataType_ReturnsBadRequest()
    {
        // Arrange
        var policyType = PolicyType.ResetPassword;
        var request = new SavePolicyRequest
        {
            Policy = new PolicyRequestModel
            {
                Type = policyType,
                Enabled = true,
                Data = new Dictionary<string, object>
                {
                    { "autoEnrollEnabled", 123 } // Wrong type - should be bool
                }
            }
        };

        // Act
        var response = await _client.PutAsync($"/organizations/{_organization.Id}/policies/{policyType}/vnext",
            JsonContent.Create(request));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
