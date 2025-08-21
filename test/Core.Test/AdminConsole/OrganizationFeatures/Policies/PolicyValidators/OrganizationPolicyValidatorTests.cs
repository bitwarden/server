using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

[SutProviderCustomize]
public class OrganizationPolicyValidatorTests
{
    [Theory, BitAutoData]
    public async Task GetUserPolicyRequirementsByOrganizationIdAsync_WithNoFactory_ThrowsNotImplementedException(
        Guid organizationId,
        SutProvider<TestOrganizationPolicyValidator> sutProvider)
    {
        // Arrange
        var sut = new TestOrganizationPolicyValidator(sutProvider.GetDependency<IPolicyRepository>(), []);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() =>
            sut.TestGetUserPolicyRequirementsByOrganizationIdAsync<TestPolicyRequirement>(
                organizationId, PolicyType.TwoFactorAuthentication));

        Assert.Contains("No Requirement Factory found for", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task GetUserPolicyRequirementsByOrganizationIdAsync_WithMultipleUsers_GroupsByUserId(
        Guid organizationId,
        Guid userId1,
        Guid userId2,
        SutProvider<TestOrganizationPolicyValidator> sutProvider)
    {
        // Arrange
        var policyDetails = new List<OrganizationPolicyDetails>
        {
            new() { UserId = userId1, OrganizationId = organizationId },
            new() { UserId = userId1, OrganizationId = organizationId },
            new() { UserId = userId2, OrganizationId = organizationId }
        };

        var factory = Substitute.For<IPolicyRequirementFactory<TestPolicyRequirement>>();
        factory.Create(Arg.Any<IEnumerable<PolicyDetails>>()).Returns(new TestPolicyRequirement());
        factory.Enforce(Arg.Any<PolicyDetails>()).Returns(true);

        sutProvider.GetDependency<IPolicyRepository>()
            .GetPolicyDetailsByOrganizationIdAsync(organizationId, PolicyType.TwoFactorAuthentication)
            .Returns(policyDetails);

        var factories = new List<IPolicyRequirementFactory<IPolicyRequirement>> { factory };
        var sut = new TestOrganizationPolicyValidator(sutProvider.GetDependency<IPolicyRepository>(), factories);

        // Act
        var result = await sut.TestGetUserPolicyRequirementsByOrganizationIdAsync<TestPolicyRequirement>(
            organizationId, PolicyType.TwoFactorAuthentication);

        // Assert
        Assert.Equal(2, result.Count());

        factory.Received(2).Create(Arg.Any<IEnumerable<PolicyDetails>>());
    }

    [Theory, BitAutoData]
    public async Task GetUserPolicyRequirementsByOrganizationIdAsync_ShouldEnforceFilters(
        Guid organizationId,
        Guid userId,
        SutProvider<TestOrganizationPolicyValidator> sutProvider)
    {
        // Arrange
        var adminUser = new OrganizationPolicyDetails()
        {
            UserId = userId,
            OrganizationId = organizationId,
            OrganizationUserType = OrganizationUserType.Admin
        };

        var user = new OrganizationPolicyDetails()
        {
            UserId = userId,
            OrganizationId = organizationId,
            OrganizationUserType = OrganizationUserType.User
        };

        var policyDetails = new List<OrganizationPolicyDetails>
        {
            adminUser,
            user
        };
        sutProvider.GetDependency<IPolicyRepository>()
            .GetPolicyDetailsByOrganizationIdAsync(organizationId, PolicyType.TwoFactorAuthentication)
            .Returns(policyDetails);

        var factory = Substitute.For<IPolicyRequirementFactory<TestPolicyRequirement>>();
        factory.Create(Arg.Any<IEnumerable<PolicyDetails>>()).Returns(new TestPolicyRequirement());
        factory.Enforce(Arg.Is<PolicyDetails>(p => p.OrganizationUserType == OrganizationUserType.Admin))
            .Returns(true);
        factory.Enforce(Arg.Is<PolicyDetails>(p => p.OrganizationUserType == OrganizationUserType.User))
            .Returns(false);

        var factories = new List<IPolicyRequirementFactory<IPolicyRequirement>> { factory };
        var sut = new TestOrganizationPolicyValidator(sutProvider.GetDependency<IPolicyRepository>(), factories);

        // Act
        var result = await sut.TestGetUserPolicyRequirementsByOrganizationIdAsync<TestPolicyRequirement>(
            organizationId, PolicyType.TwoFactorAuthentication);

        // Assert
        Assert.Single(result);

        factory.Received(1).Create(Arg.Is<IEnumerable<PolicyDetails>>(policies =>
            policies.Count() == 1 && policies.First().OrganizationUserType == OrganizationUserType.Admin));

        factory.Received(1).Enforce(Arg.Is<PolicyDetails>(p => ReferenceEquals(p, adminUser)));
        factory.Received(1).Enforce(Arg.Is<PolicyDetails>(p => ReferenceEquals(p, user)));
        factory.Received(2).Enforce(Arg.Any<OrganizationPolicyDetails>());
    }

    [Theory, BitAutoData]
    public async Task GetUserPolicyRequirementsByOrganizationIdAsync_WithEmptyPolicyDetails_ReturnsEmptyCollection(
        Guid organizationId,
        SutProvider<TestOrganizationPolicyValidator> sutProvider)
    {
        // Arrange
        var factory = Substitute.For<IPolicyRequirementFactory<TestPolicyRequirement>>();

        sutProvider.GetDependency<IPolicyRepository>()
            .GetPolicyDetailsByOrganizationIdAsync(organizationId, PolicyType.TwoFactorAuthentication)
            .Returns(new List<OrganizationPolicyDetails>());

        var factories = new List<IPolicyRequirementFactory<IPolicyRequirement>> { factory };
        var sut = new TestOrganizationPolicyValidator(sutProvider.GetDependency<IPolicyRepository>(), factories);

        // Act
        var result = await sut.TestGetUserPolicyRequirementsByOrganizationIdAsync<TestPolicyRequirement>(
            organizationId, PolicyType.TwoFactorAuthentication);

        // Assert
        Assert.Empty(result);
        factory.DidNotReceive().Create(Arg.Any<IEnumerable<PolicyDetails>>());
    }
}

public class TestOrganizationPolicyValidator : OrganizationPolicyValidator
{
    public TestOrganizationPolicyValidator(
        IPolicyRepository policyRepository,
        IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>>? factories = null)
        : base(policyRepository, factories ?? [])
    {
    }

    public override PolicyType Type => PolicyType.TwoFactorAuthentication;

    public override IEnumerable<PolicyType> RequiredPolicies => [];

    public override Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        return Task.FromResult("");
    }

    public override Task OnSaveSideEffectsAsync(SavePolicyModel policyModel, Policy? currentPolicy) => throw new NotImplementedException();

    public override Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<T>> TestGetUserPolicyRequirementsByOrganizationIdAsync<T>(Guid organizationId, PolicyType policyType)
        where T : IPolicyRequirement
    {
        return await GetUserPolicyRequirementsByOrganizationIdAsync<T>(organizationId, policyType);
    }

}

public class TestPolicyRequirement : IPolicyRequirement
{
}
