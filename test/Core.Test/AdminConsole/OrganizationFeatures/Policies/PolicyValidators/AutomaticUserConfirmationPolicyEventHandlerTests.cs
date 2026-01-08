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
        Guid nonCompliantUserId,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
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
        Guid userId,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        var orgUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = policyUpdate.OrganizationId,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Confirmed,
            UserId = userId,
        };

        var otherOrgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            UserId = null, // invited users do not have a user id
            Status = OrganizationUserStatusType.Invited,
            Email = orgUser.Email
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([orgUser]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([otherOrgUser]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.True(string.IsNullOrEmpty(result));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_ProviderUsersExist_ReturnsError(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        Guid userId,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        var orgUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = policyUpdate.OrganizationId,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Confirmed,
            UserId = userId
        };

        var providerUser = new ProviderUser
        {
            Id = Guid.NewGuid(),
            ProviderId = Guid.NewGuid(),
            UserId = userId,
            Status = ProviderUserStatusType.Confirmed
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([orgUser]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([providerUser]);

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
        var orgUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = policyUpdate.OrganizationId,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Confirmed,
            UserId = Guid.NewGuid()
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([orgUser]);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
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

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceive()
            .GetManyDetailsByOrganizationAsync(Arg.Any<Guid>());
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
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceive()
            .GetManyDetailsByOrganizationAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_IncludesOwnersAndAdmins_InComplianceCheck(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        Guid nonCompliantOwnerId,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        var ownerUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = policyUpdate.OrganizationId,
            Type = OrganizationUserType.Owner,
            Status = OrganizationUserStatusType.Confirmed,
            UserId = nonCompliantOwnerId,
        };

        var otherOrgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            UserId = nonCompliantOwnerId,
            Status = OrganizationUserStatusType.Confirmed
        };

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
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        var invitedUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = policyUpdate.OrganizationId,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Invited,
            UserId = Guid.NewGuid(),
            Email = "invited@example.com"
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([invitedUser]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.True(string.IsNullOrEmpty(result));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_RevokedUsersIncluded_InComplianceCheck(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        var revokedUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = policyUpdate.OrganizationId,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Revoked,
            UserId = Guid.NewGuid(),
        };

        var additionalOrgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Revoked,
            UserId = revokedUser.UserId,
        };

        var orgUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        orgUserRepository
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([revokedUser]);

        orgUserRepository.GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([additionalOrgUser]);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Assert
        Assert.Contains("compliant with the Single organization policy", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_AcceptedUsersIncluded_InComplianceCheck(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        Guid nonCompliantUserId,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        var acceptedUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = policyUpdate.OrganizationId,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Accepted,
            UserId = nonCompliantUserId,
        };

        var otherOrgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            UserId = nonCompliantUserId,
            Status = OrganizationUserStatusType.Confirmed
        };

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
    public async Task ValidateAsync_WithSavePolicyModel_CallsValidateWithPolicyUpdate(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        SutProvider<AutomaticUserConfirmationPolicyEventHandler> sutProvider)
    {
        // Arrange
        var savePolicyModel = new SavePolicyModel(policyUpdate);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([]);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(savePolicyModel, null);

        // Assert
        Assert.True(string.IsNullOrEmpty(result));
    }
}
