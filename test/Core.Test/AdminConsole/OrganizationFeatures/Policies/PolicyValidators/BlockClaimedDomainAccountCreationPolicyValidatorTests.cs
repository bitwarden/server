namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

[SutProviderCustomize]
public class BlockClaimedDomainAccountCreationPolicyValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_NoVerifiedDomains_ValidationError(
        [PolicyUpdate(PolicyType.BlockClaimedDomainAccountCreation, true)] PolicyUpdate policyUpdate,
        SutProvider<BlockClaimedDomainAccountCreationPolicyValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.BlockClaimedDomainAccountCreation)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationHasVerifiedDomainsQuery>()
            .HasVerifiedDomainsAsync(policyUpdate.OrganizationId)
            .Returns(false);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.Equal("You must claim at least one domain to turn on this policy", result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_HasVerifiedDomains_Success(
        [PolicyUpdate(PolicyType.BlockClaimedDomainAccountCreation, true)] PolicyUpdate policyUpdate,
        SutProvider<BlockClaimedDomainAccountCreationPolicyValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.BlockClaimedDomainAccountCreation)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationHasVerifiedDomainsQuery>()
            .HasVerifiedDomainsAsync(policyUpdate.OrganizationId)
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.True(string.IsNullOrEmpty(result));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DisablingPolicy_NoValidation(
        [PolicyUpdate(PolicyType.BlockClaimedDomainAccountCreation, false)] PolicyUpdate policyUpdate,
        SutProvider<BlockClaimedDomainAccountCreationPolicyValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.BlockClaimedDomainAccountCreation)
            .Returns(true);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.True(string.IsNullOrEmpty(result));
        await sutProvider.GetDependency<IOrganizationHasVerifiedDomainsQuery>()
            .DidNotReceive()
            .HasVerifiedDomainsAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithSavePolicyModel_EnablingPolicy_NoVerifiedDomains_ValidationError(
        [PolicyUpdate(PolicyType.BlockClaimedDomainAccountCreation, true)] PolicyUpdate policyUpdate,
        SutProvider<BlockClaimedDomainAccountCreationPolicyValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.BlockClaimedDomainAccountCreation)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationHasVerifiedDomainsQuery>()
            .HasVerifiedDomainsAsync(policyUpdate.OrganizationId)
            .Returns(false);

        var savePolicyModel = new SavePolicyModel(policyUpdate, null, new EmptyMetadataModel());

        // Act
        var result = await sutProvider.Sut.ValidateAsync(savePolicyModel, null);

        // Assert
        Assert.Equal("You must claim at least one domain to turn on this policy", result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithSavePolicyModel_EnablingPolicy_HasVerifiedDomains_Success(
        [PolicyUpdate(PolicyType.BlockClaimedDomainAccountCreation, true)] PolicyUpdate policyUpdate,
        SutProvider<BlockClaimedDomainAccountCreationPolicyValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.BlockClaimedDomainAccountCreation)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationHasVerifiedDomainsQuery>()
            .HasVerifiedDomainsAsync(policyUpdate.OrganizationId)
            .Returns(true);

        var savePolicyModel = new SavePolicyModel(policyUpdate, null, new EmptyMetadataModel());

        // Act
        var result = await sutProvider.Sut.ValidateAsync(savePolicyModel, null);

        // Assert
        Assert.True(string.IsNullOrEmpty(result));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithSavePolicyModel_DisablingPolicy_NoValidation(
        [PolicyUpdate(PolicyType.BlockClaimedDomainAccountCreation, false)] PolicyUpdate policyUpdate,
        SutProvider<BlockClaimedDomainAccountCreationPolicyValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.BlockClaimedDomainAccountCreation)
            .Returns(true);

        var savePolicyModel = new SavePolicyModel(policyUpdate, null, new EmptyMetadataModel());

        // Act
        var result = await sutProvider.Sut.ValidateAsync(savePolicyModel, null);

        // Assert
        Assert.True(string.IsNullOrEmpty(result));
        await sutProvider.GetDependency<IOrganizationHasVerifiedDomainsQuery>()
            .DidNotReceive()
            .HasVerifiedDomainsAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_FeatureFlagDisabled_ReturnsError(
        [PolicyUpdate(PolicyType.BlockClaimedDomainAccountCreation, true)] PolicyUpdate policyUpdate,
        SutProvider<BlockClaimedDomainAccountCreationPolicyValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.BlockClaimedDomainAccountCreation)
            .Returns(false);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.Equal("This feature is not enabled", result);
        await sutProvider.GetDependency<IOrganizationHasVerifiedDomainsQuery>()
            .DidNotReceive()
            .HasVerifiedDomainsAsync(Arg.Any<Guid>());
    }

    [Fact]
    public void Type_ReturnsBlockClaimedDomainAccountCreation()
    {
        // Arrange
        var validator = new BlockClaimedDomainAccountCreationPolicyValidator(null, null);

        // Act & Assert
        Assert.Equal(PolicyType.BlockClaimedDomainAccountCreation, validator.Type);
    }

    [Fact]
    public void RequiredPolicies_ReturnsEmpty()
    {
        // Arrange
        var validator = new BlockClaimedDomainAccountCreationPolicyValidator(null, null);

        // Act
        var requiredPolicies = validator.RequiredPolicies.ToList();

        // Assert
        Assert.Empty(requiredPolicies);
    }
}
