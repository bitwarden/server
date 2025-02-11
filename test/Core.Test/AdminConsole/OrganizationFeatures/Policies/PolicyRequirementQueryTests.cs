using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Test.Common.AutoFixture;
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
    public async Task GetAsync_Works(Guid userId, Guid organizationId, SutProvider<TestPolicyRequirementQuery> sutProvider)
    {
        sutProvider.GetDependency<IPolicyRepository>().GetPolicyDetailsByUserId(userId).Returns([
            new PolicyDetails
            {
                OrganizationId = organizationId
            }
        ]);

        var requirement = await sutProvider.Sut.GetAsync<TestPolicyRequirement>(userId);
        Assert.Equal(organizationId, requirement.OrganizationId);
    }

    [Theory, BitAutoData]
    public async Task GetAsync_ThrowsIfNoRequirementRegistered(Guid userId, SutProvider<PolicyRequirementQuery> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<NotImplementedException>(()
            => sutProvider.Sut.GetAsync<TestPolicyRequirement>(userId));
        Assert.Contains("No Policy Requirement found", exception.Message);
    }

    /// <summary>
    /// Test query used to register our own TestPolicyRequirement so that we're testing the query itself
    /// decoupled from any real requirement that is registered from time to time.
    /// </summary>
    public class TestPolicyRequirementQuery : PolicyRequirementQuery
    {
        public TestPolicyRequirementQuery(IPolicyRepository policyRepository) : base(policyRepository)
        {
            PolicyRequirements.Add(TestPolicyRequirement.Create);
        }
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
