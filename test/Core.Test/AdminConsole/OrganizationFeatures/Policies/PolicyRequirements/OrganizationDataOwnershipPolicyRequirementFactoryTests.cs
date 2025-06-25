﻿using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

[SutProviderCustomize]
public class OrganizationDataOwnershipPolicyRequirementFactoryTests
{
    [Theory, BitAutoData]
    public void State_WithNoPolicies_ReturnsAllowed(SutProvider<OrganizationDataOwnershipPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.Equal(OrganizationDataOwnershipState.Disabled, actual.State);
    }

    [Theory, BitAutoData]
    public void State_WithOrganizationDataOwnershipPolicies_ReturnsRestricted(
        [PolicyDetails(PolicyType.OrganizationDataOwnership)] PolicyDetails[] policies,
        SutProvider<OrganizationDataOwnershipPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(policies);

        Assert.Equal(OrganizationDataOwnershipState.Enabled, actual.State);
    }

    [Theory, BitAutoData]
    public void RequiresDefaultCollection_WithNoPolicies_ReturnsFalse(
        Guid organizationId,
        SutProvider<OrganizationDataOwnershipPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.RequiresDefaultCollection(organizationId));
    }

    [Theory, BitAutoData]
    public void RequiresDefaultCollection_WithOrganizationDataOwnershipPolicies_ReturnsCorrectResult(
        [PolicyDetails(PolicyType.OrganizationDataOwnership)] PolicyDetails[] policies,
        Guid nonPolicyOrganizationId,
        SutProvider<OrganizationDataOwnershipPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.RequiresDefaultCollection(policies[0].OrganizationId));
        Assert.False(actual.RequiresDefaultCollection(nonPolicyOrganizationId));
    }
}
