using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies;

[SutProviderCustomize]
public class PolicyRequirementQueryTests
{
    /// <summary>
    /// Tests that the query correctly registers, retrieves and instantiates arbitrary IPolicyRequirements
    /// according to their provided CreateRequirement delegate.
    /// </summary>
    [Theory, BitAutoData]
    public async Task GetAsync_Works(Guid userId, Guid organizationId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var factories = new List<RequirementFactory<IPolicyRequirement>>
        {
            // In prod this cast is handled when the CreateRequirement delegate is registered in DI
            (RequirementFactory<TestPolicyRequirement>)TestPolicyRequirement.Create
        };

        var sut = new PolicyRequirementQuery(policyRepository, factories);
        policyRepository.GetPolicyDetailsByUserId(userId).Returns([
            new PolicyDetails
            {
                OrganizationId = organizationId
            }
        ]);

        var requirement = await sut.GetAsync<TestPolicyRequirement>(userId);
        Assert.Equal(organizationId, requirement.OrganizationId);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ThrowsIfNoRequirementRegistered(Guid userId)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();
        var sut = new PolicyRequirementQuery(policyRepository, []);

        var exception = await Assert.ThrowsAsync<NotImplementedException>(()
            => sut.GetAsync<TestPolicyRequirement>(userId));
        Assert.Contains("No Policy Requirement found", exception.Message);
    }

    /// <summary>
    /// Intentionally simplified PolicyRequirement that just holds the Policy.OrganizationId for us to assert against.
    /// </summary>
    private class TestPolicyRequirement : IPolicyRequirement
    {
        public Guid OrganizationId { get; init; }
        public static TestPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
            => new() { OrganizationId = policyDetails.Single().OrganizationId };
    }
}
