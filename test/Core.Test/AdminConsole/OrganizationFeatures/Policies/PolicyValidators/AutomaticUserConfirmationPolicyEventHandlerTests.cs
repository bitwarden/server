using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

[SutProviderCustomize]
public class AutomaticUserConfirmationPolicyEventHandlerTests
{
    [Theory, BitAutoData]
    public void RequiredPolicies_IncludesSingleOrg(
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Act
        var requiredPolicies = sutProvider.Sut.RequiredPolicies;

        // Assert
        Assert.Contains(PolicyType.SingleOrg, requiredPolicies);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_UsersNotCompliantWithSingleOrg_ReturnsError(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(policyUpdate.OrganizationId);

        sutProvider.GetDependency<IAutomaticUserConfirmationOrganizationPolicyComplianceValidator>()
            .IsOrganizationCompliantAsync(Arg.Any<AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest>())
            .Returns(Invalid(request, new UserNotCompliantWithSingleOrganization()));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.Contains("compliant with the Single organization policy", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_ProviderUsersExist_ReturnsError(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(policyUpdate.OrganizationId);

        sutProvider.GetDependency<IAutomaticUserConfirmationOrganizationPolicyComplianceValidator>()
            .IsOrganizationCompliantAsync(Arg.Any<AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest>())
            .Returns(Invalid(request, new ProviderExistsInOrganization()));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.Contains("Provider user type", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_AllValidationsPassed_ReturnsEmptyString(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(policyUpdate.OrganizationId);

        sutProvider.GetDependency<IAutomaticUserConfirmationOrganizationPolicyComplianceValidator>()
            .IsOrganizationCompliantAsync(Arg.Any<AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest>())
            .Returns(Valid(request));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.True(string.IsNullOrEmpty(result));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_PolicyAlreadyEnabled_ReturnsEmptyString(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.AutomaticUserConfirmation)] Policy currentPolicy,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        currentPolicy.OrganizationId = policyUpdate.OrganizationId;

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, currentPolicy);

        // Assert
        Assert.True(string.IsNullOrEmpty(result));

        await sutProvider.GetDependency<IAutomaticUserConfirmationOrganizationPolicyComplianceValidator>()
            .DidNotReceive()
            .IsOrganizationCompliantAsync(Arg.Any<AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest>());
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DisablingPolicy_ReturnsEmptyString(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation, false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.AutomaticUserConfirmation)] Policy currentPolicy,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        currentPolicy.OrganizationId = policyUpdate.OrganizationId;

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, currentPolicy);

        // Assert
        Assert.True(string.IsNullOrEmpty(result));
        await sutProvider.GetDependency<IAutomaticUserConfirmationOrganizationPolicyComplianceValidator>()
            .DidNotReceive()
            .IsOrganizationCompliantAsync(Arg.Any<AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest>());
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_PassesCorrectOrganizationId(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(policyUpdate.OrganizationId);

        sutProvider.GetDependency<IAutomaticUserConfirmationOrganizationPolicyComplianceValidator>()
            .IsOrganizationCompliantAsync(Arg.Any<AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest>())
            .Returns(Valid(request));

        // Act
        await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        await sutProvider.GetDependency<IAutomaticUserConfirmationOrganizationPolicyComplianceValidator>()
            .Received(1)
            .IsOrganizationCompliantAsync(Arg.Is<AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest>(
                r => r.OrganizationId == policyUpdate.OrganizationId));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithSavePolicyModel_CallsValidateWithPolicyUpdate(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        var savePolicyModel = new SavePolicyModel(policyUpdate);
        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(policyUpdate.OrganizationId);

        sutProvider.GetDependency<IAutomaticUserConfirmationOrganizationPolicyComplianceValidator>()
            .IsOrganizationCompliantAsync(Arg.Any<AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest>())
            .Returns(Valid(request));

        // Act
        var result = await sutProvider.Sut.ValidateAsync(savePolicyModel, null);

        // Assert
        Assert.True(string.IsNullOrEmpty(result));
    }
}
