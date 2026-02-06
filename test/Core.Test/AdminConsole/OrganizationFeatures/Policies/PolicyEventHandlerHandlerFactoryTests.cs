using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using OneOf.Types;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies;

public class PolicyEventHandlerHandlerFactoryTests
{
    [Fact]
    public void GetHandler_ReturnsHandler_WhenHandlerExists()
    {
        // Arrange
        var expectedHandler = new FakeSingleOrgDependencyEvent();
        var factory = new PolicyEventHandlerHandlerFactory([expectedHandler]);

        // Act
        var result = factory.GetHandler<IEnforceDependentPoliciesEvent>(PolicyType.SingleOrg);

        // Assert
        Assert.True(result.IsT0);
        Assert.Equal(expectedHandler, result.AsT0);
    }

    [Fact]
    public void GetHandler_ReturnsNone_WhenHandlerDoesNotExist()
    {
        // Arrange
        var factory = new PolicyEventHandlerHandlerFactory([new FakeSingleOrgDependencyEvent()]);

        // Act
        var result = factory.GetHandler<IEnforceDependentPoliciesEvent>(PolicyType.RequireSso);

        // Assert
        Assert.True(result.IsT1);
        Assert.IsType<None>(result.AsT1);
    }

    [Fact]
    public void GetHandler_ReturnsNone_WhenHandlerTypeDoesNotMatch()
    {
        // Arrange
        var factory = new PolicyEventHandlerHandlerFactory([new FakeSingleOrgDependencyEvent()]);

        // Act
        var result = factory.GetHandler<IPolicyValidationEvent>(PolicyType.SingleOrg);

        // Assert
        Assert.True(result.IsT1);
        Assert.IsType<None>(result.AsT1);
    }

    [Fact]
    public void GetHandler_ReturnsCorrectHandler_WhenMultipleHandlerTypesExist()
    {
        // Arrange
        var dependencyEvent = new FakeSingleOrgDependencyEvent();
        var validationEvent = new FakeSingleOrgValidationEvent();
        var factory = new PolicyEventHandlerHandlerFactory([dependencyEvent, validationEvent]);

        // Act
        var dependencyResult = factory.GetHandler<IEnforceDependentPoliciesEvent>(PolicyType.SingleOrg);
        var validationResult = factory.GetHandler<IPolicyValidationEvent>(PolicyType.SingleOrg);

        // Assert
        Assert.True(dependencyResult.IsT0);
        Assert.Equal(dependencyEvent, dependencyResult.AsT0);

        Assert.True(validationResult.IsT0);
        Assert.Equal(validationEvent, validationResult.AsT0);
    }

    [Fact]
    public void GetHandler_ReturnsCorrectHandler_WhenMultiplePolicyTypesExist()
    {
        // Arrange
        var singleOrgEvent = new FakeSingleOrgDependencyEvent();
        var requireSsoEvent = new FakeRequireSsoDependencyEvent();
        var factory = new PolicyEventHandlerHandlerFactory([singleOrgEvent, requireSsoEvent]);

        // Act
        var singleOrgResult = factory.GetHandler<IEnforceDependentPoliciesEvent>(PolicyType.SingleOrg);
        var requireSsoResult = factory.GetHandler<IEnforceDependentPoliciesEvent>(PolicyType.RequireSso);

        // Assert
        Assert.True(singleOrgResult.IsT0);
        Assert.Equal(singleOrgEvent, singleOrgResult.AsT0);

        Assert.True(requireSsoResult.IsT0);
        Assert.Equal(requireSsoEvent, requireSsoResult.AsT0);
    }

    [Fact]
    public void GetHandler_Throws_WhenDuplicateHandlersExist()
    {
        // Arrange
        var factory = new PolicyEventHandlerHandlerFactory([
            new FakeSingleOrgDependencyEvent(),
            new FakeSingleOrgDependencyEvent()
        ]);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            factory.GetHandler<IEnforceDependentPoliciesEvent>(PolicyType.SingleOrg));

        Assert.Contains("Multiple IPolicyUpdateEvent handlers of type IEnforceDependentPoliciesEvent found for PolicyType SingleOrg", exception.Message);
        Assert.Contains("Expected one IEnforceDependentPoliciesEvent handler per PolicyType", exception.Message);
    }

    [Fact]
    public void GetHandler_ReturnsNone_WhenNoHandlersProvided()
    {
        // Arrange
        var factory = new PolicyEventHandlerHandlerFactory([]);

        // Act
        var result = factory.GetHandler<IEnforceDependentPoliciesEvent>(PolicyType.SingleOrg);

        // Assert
        Assert.True(result.IsT1);
        Assert.IsType<None>(result.AsT1);
    }
}
