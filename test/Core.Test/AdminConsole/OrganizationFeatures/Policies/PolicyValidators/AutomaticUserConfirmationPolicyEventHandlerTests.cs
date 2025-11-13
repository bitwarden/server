using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

[SutProviderCustomize]
public class AutomaticUserConfirmationPolicyEventHandlerTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_SingleOrgNotEnabled_ReturnsError(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SingleOrg)
            .Returns((Policy?)null);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.Contains("Single organization policy must be enabled", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_SingleOrgPolicyDisabled_ReturnsError(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg, false)] Policy singleOrgPolicy,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        singleOrgPolicy.OrganizationId = policyUpdate.OrganizationId;

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SingleOrg)
            .Returns(singleOrgPolicy);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.Contains("Single organization policy must be enabled", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_UsersNotCompliantWithSingleOrg_ReturnsError(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        Guid nonCompliantUserId,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        singleOrgPolicy.OrganizationId = policyUpdate.OrganizationId;

        var orgUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = policyUpdate.OrganizationId,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Confirmed,
            UserId = nonCompliantUserId,
            Email = "user@example.com"
        };

        var otherOrgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            UserId = nonCompliantUserId,
            Status = OrganizationUserStatusType.Confirmed
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SingleOrg)
            .Returns(singleOrgPolicy);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([orgUser]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([otherOrgUser]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.Contains("compliant with the Single organization policy", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_UserWithInvitedStatusInOtherOrg_ValidationPasses(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        Guid userId,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        singleOrgPolicy.OrganizationId = policyUpdate.OrganizationId;

        var orgUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = policyUpdate.OrganizationId,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Confirmed,
            UserId = userId,
            Email = "test@email.com"
        };

        var otherOrgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            UserId = null, // invited users do not have a user id
            Status = OrganizationUserStatusType.Invited,
            Email = orgUser.Email
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SingleOrg)
            .Returns(singleOrgPolicy);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([orgUser]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([otherOrgUser]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.True(string.IsNullOrEmpty(result));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_ProviderUsersExist_ReturnsError(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        singleOrgPolicy.OrganizationId = policyUpdate.OrganizationId;

        var providerUser = new ProviderUser
        {
            Id = Guid.NewGuid(),
            ProviderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = ProviderUserStatusType.Confirmed
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SingleOrg)
            .Returns(singleOrgPolicy);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([providerUser]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.Contains("Provider user type", result, StringComparison.OrdinalIgnoreCase);
    }


    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_AllValidationsPassed_ReturnsEmptyString(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        singleOrgPolicy.OrganizationId = policyUpdate.OrganizationId;

        var orgUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = policyUpdate.OrganizationId,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Confirmed,
            UserId = Guid.NewGuid(),
            Email = "user@example.com"
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SingleOrg)
            .Returns(singleOrgPolicy);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([orgUser]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([]);

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
        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceive()
            .GetByOrganizationIdTypeAsync(Arg.Any<Guid>(), Arg.Any<PolicyType>());
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
        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceive()
            .GetByOrganizationIdTypeAsync(Arg.Any<Guid>(), Arg.Any<PolicyType>());
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_IncludesOwnersAndAdmins_InComplianceCheck(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        Guid nonCompliantOwnerId,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        singleOrgPolicy.OrganizationId = policyUpdate.OrganizationId;

        var ownerUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = policyUpdate.OrganizationId,
            Type = OrganizationUserType.Owner,
            Status = OrganizationUserStatusType.Confirmed,
            UserId = nonCompliantOwnerId,
            Email = "owner@example.com"
        };

        var otherOrgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            UserId = nonCompliantOwnerId,
            Status = OrganizationUserStatusType.Confirmed
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SingleOrg)
            .Returns(singleOrgPolicy);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([ownerUser]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([otherOrgUser]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.Contains("compliant with the Single organization policy", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_InvitedUsersExcluded_FromComplianceCheck(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        singleOrgPolicy.OrganizationId = policyUpdate.OrganizationId;

        var invitedUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = policyUpdate.OrganizationId,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Invited,
            UserId = Guid.NewGuid(),
            Email = "invited@example.com"
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SingleOrg)
            .Returns(singleOrgPolicy);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([invitedUser]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.True(string.IsNullOrEmpty(result));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_RevokedUsersExcluded_FromComplianceCheck(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        singleOrgPolicy.OrganizationId = policyUpdate.OrganizationId;

        var revokedUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = policyUpdate.OrganizationId,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Revoked,
            UserId = Guid.NewGuid(),
            Email = "revoked@example.com"
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SingleOrg)
            .Returns(singleOrgPolicy);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([revokedUser]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.True(string.IsNullOrEmpty(result));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_AcceptedUsersIncluded_InComplianceCheck(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        Guid nonCompliantUserId,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        singleOrgPolicy.OrganizationId = policyUpdate.OrganizationId;

        var acceptedUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = policyUpdate.OrganizationId,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Accepted,
            UserId = nonCompliantUserId,
            Email = "accepted@example.com"
        };

        var otherOrgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            UserId = nonCompliantUserId,
            Status = OrganizationUserStatusType.Confirmed
        };

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SingleOrg)
            .Returns(singleOrgPolicy);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([acceptedUser]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([otherOrgUser]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.Contains("compliant with the Single organization policy", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_EmptyOrganization_ReturnsEmptyString(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        singleOrgPolicy.OrganizationId = policyUpdate.OrganizationId;

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SingleOrg)
            .Returns(singleOrgPolicy);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.True(string.IsNullOrEmpty(result));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_WithSavePolicyModel_CallsValidateWithPolicyUpdate(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        singleOrgPolicy.OrganizationId = policyUpdate.OrganizationId;

        var savePolicyModel = new SavePolicyModel(policyUpdate);

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SingleOrg)
            .Returns(singleOrgPolicy);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(savePolicyModel, null);

        // Assert
        Assert.True(string.IsNullOrEmpty(result));
    }

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_EnablingPolicy_SetsUseAutomaticUserConfirmationToTrue(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        Organization organization,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        organization.Id = policyUpdate.OrganizationId;
        organization.UseAutomaticUserConfirmation = false;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(policyUpdate.OrganizationId)
            .Returns(organization);

        // Act
        await sutProvider.Sut.OnSaveSideEffectsAsync(policyUpdate, null);

        // Assert
        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Organization>(o =>
                o.Id == organization.Id &&
                o.UseAutomaticUserConfirmation == true &&
                o.RevisionDate > DateTime.MinValue));
    }

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_DisablingPolicy_SetsUseAutomaticUserConfirmationToFalse(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation, false)] PolicyUpdate policyUpdate,
        Organization organization,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        organization.Id = policyUpdate.OrganizationId;
        organization.UseAutomaticUserConfirmation = true;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(policyUpdate.OrganizationId)
            .Returns(organization);

        // Act
        await sutProvider.Sut.OnSaveSideEffectsAsync(policyUpdate, null);

        // Assert
        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Organization>(o =>
                o.Id == organization.Id &&
                o.UseAutomaticUserConfirmation == false &&
                o.RevisionDate > DateTime.MinValue));
    }

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_OrganizationNotFound_DoesNotThrowException(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(policyUpdate.OrganizationId)
            .Returns((Organization?)null);

        // Act
        await sutProvider.Sut.OnSaveSideEffectsAsync(policyUpdate, null);

        // Assert
        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .UpsertAsync(Arg.Any<Organization>());
    }

    [Theory, BitAutoData]
    public async Task ExecutePreUpsertSideEffectAsync_CallsOnSaveSideEffectsAsync(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.AutomaticUserConfirmation)] Policy currentPolicy,
        Organization organization,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        organization.Id = policyUpdate.OrganizationId;
        currentPolicy.OrganizationId = policyUpdate.OrganizationId;

        var savePolicyModel = new SavePolicyModel(policyUpdate);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(policyUpdate.OrganizationId)
            .Returns(organization);

        // Act
        await sutProvider.Sut.ExecutePreUpsertSideEffectAsync(savePolicyModel, currentPolicy);

        // Assert
        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Organization>(o =>
                o.Id == organization.Id &&
                o.UseAutomaticUserConfirmation == policyUpdate.Enabled));
    }

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_UpdatesRevisionDate(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        Organization organization,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        organization.Id = policyUpdate.OrganizationId;
        var originalRevisionDate = DateTime.UtcNow.AddDays(-1);
        organization.RevisionDate = originalRevisionDate;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(policyUpdate.OrganizationId)
            .Returns(organization);

        // Act
        await sutProvider.Sut.OnSaveSideEffectsAsync(policyUpdate, null);

        // Assert
        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .UpsertAsync(Arg.Is<Organization>(o =>
                o.Id == organization.Id &&
                o.RevisionDate > originalRevisionDate));
    }
}
