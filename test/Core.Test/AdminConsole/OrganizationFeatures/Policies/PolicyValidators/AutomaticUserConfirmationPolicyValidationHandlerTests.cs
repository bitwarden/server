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
public class AutomaticUserConfirmationPolicyValidationHandlerTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_SingleOrgNotEnabled_ReturnsError(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        SutProvider<AutomaticUserConfirmationPolicyValidationHandler> sutProvider)
    {
        // Single Org policy is not enabled
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SingleOrg)
            .Returns((Policy?)null);

        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        Assert.Contains("Single organization policy must be enabled", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_SingleOrgPolicyDisabled_ReturnsError(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg, false)] Policy singleOrgPolicy,
        SutProvider<AutomaticUserConfirmationPolicyValidationHandler> sutProvider)
    {
        singleOrgPolicy.OrganizationId = policyUpdate.OrganizationId;

        // Single Org policy exists but is disabled
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policyUpdate.OrganizationId, PolicyType.SingleOrg)
            .Returns(singleOrgPolicy);

        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        Assert.Contains("Single organization policy must be enabled", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_UsersNotCompliantWithSingleOrg_ReturnsError(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        Guid nonCompliantUserId,
        SutProvider<AutomaticUserConfirmationPolicyValidationHandler> sutProvider)
    {
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
            OrganizationId = Guid.NewGuid(), // Different organization
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

        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        Assert.Contains("compliant with the Single organization policy", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_UserWithInvitedStatusInOtherOrg_ValidationPasses(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        Guid userId,
        SutProvider<AutomaticUserConfirmationPolicyValidationHandler> sutProvider)
    {
        singleOrgPolicy.OrganizationId = policyUpdate.OrganizationId;

        var orgUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = policyUpdate.OrganizationId,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Confirmed,
            UserId = userId,
            Email = "user@example.com"
        };

        // User has invited status in another organization (should not count as non-compliant)
        var otherOrgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(), // Different organization
            UserId = userId,
            Status = OrganizationUserStatusType.Invited
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

        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Invited users in other orgs should not make validation fail
        Assert.True(string.IsNullOrEmpty(result));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_ProviderUsersExist_ReturnsError(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        SutProvider<AutomaticUserConfirmationPolicyValidationHandler> sutProvider)
    {
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

        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        Assert.Contains("Provider user type", result, StringComparison.OrdinalIgnoreCase);
    }


    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_AllValidationsPassed_ReturnsEmptyString(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        SutProvider<AutomaticUserConfirmationPolicyValidationHandler> sutProvider)
    {
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
            .Returns([]); // No other org memberships

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([]);

        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        Assert.True(string.IsNullOrEmpty(result));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_PolicyAlreadyEnabled_ReturnsEmptyString(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.AutomaticUserConfirmation)] Policy currentPolicy,
        SutProvider<AutomaticUserConfirmationPolicyValidationHandler> sutProvider)
    {
        currentPolicy.OrganizationId = policyUpdate.OrganizationId;

        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, currentPolicy);

        Assert.True(string.IsNullOrEmpty(result));
        // Should not call any repository methods since policy is already enabled
        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceive()
            .GetByOrganizationIdTypeAsync(Arg.Any<Guid>(), Arg.Any<PolicyType>());
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DisablingPolicy_ReturnsEmptyString(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation, false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.AutomaticUserConfirmation)] Policy currentPolicy,
        SutProvider<AutomaticUserConfirmationPolicyValidationHandler> sutProvider)
    {
        currentPolicy.OrganizationId = policyUpdate.OrganizationId;

        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, currentPolicy);

        Assert.True(string.IsNullOrEmpty(result));
        // Should not call any repository methods when disabling
        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceive()
            .GetByOrganizationIdTypeAsync(Arg.Any<Guid>(), Arg.Any<PolicyType>());
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_IncludesOwnersAndAdmins_InComplianceCheck(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        Guid nonCompliantOwnerId,
        SutProvider<AutomaticUserConfirmationPolicyValidationHandler> sutProvider)
    {
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
            OrganizationId = Guid.NewGuid(), // Different organization
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

        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Owners and Admins should NOT be filtered out, so validation should fail
        Assert.Contains("compliant with the Single organization policy", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_InvitedUsersExcluded_FromComplianceCheck(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        SutProvider<AutomaticUserConfirmationPolicyValidationHandler> sutProvider)
    {
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

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByOrganizationAsync(policyUpdate.OrganizationId, null)
            .Returns([]);

        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Invited users are excluded, so validation should pass
        Assert.True(string.IsNullOrEmpty(result));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_EnablingPolicy_RevokedUsersExcluded_FromComplianceCheck(
        [PolicyUpdate(PolicyType.AutomaticUserConfirmation)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy singleOrgPolicy,
        SutProvider<AutomaticUserConfirmationPolicyValidationHandler> sutProvider)
    {
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

        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, null);

        // Revoked users are excluded, so validation should pass
        Assert.True(string.IsNullOrEmpty(result));
    }
}
