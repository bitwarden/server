﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class ConfirmOrganizationUserCommandTests
{
    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithInvalidStatus_ThrowsBadRequestException(OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Invited)] OrganizationUser orgUser, string key,
        SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        organizationUserRepository.GetByIdAsync(orgUser.Id).Returns(orgUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id));
        Assert.Contains("User not valid.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithWrongOrganization_ThrowsBadRequestException(OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, string key,
        SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        organizationUserRepository.GetByIdAsync(orgUser.Id).Returns(orgUser);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(confirmingUser.OrganizationId, orgUser.Id, key, confirmingUser.Id));
        Assert.Contains("User not valid.", exception.Message);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task ConfirmUserAsync_ToFree_WithExistingAdminOrOwner_ThrowsBadRequestException(OrganizationUserType userType, Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();

        org.PlanType = PlanType.Free;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;
        orgUser.Type = userType;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(orgUser.UserId.Value).Returns(1);
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id));
        Assert.Contains("User can only be an admin of one free organization.", exception.Message);
    }

    [Theory]
    [BitAutoData(PlanType.Custom, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.Custom, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.EnterpriseAnnually, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.EnterpriseAnnually, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.EnterpriseAnnually2020, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.EnterpriseAnnually2020, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.EnterpriseAnnually2019, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.EnterpriseAnnually2019, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.EnterpriseMonthly, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.EnterpriseMonthly, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.EnterpriseMonthly2020, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.EnterpriseMonthly2020, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.EnterpriseMonthly2019, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.EnterpriseMonthly2019, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.FamiliesAnnually, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.FamiliesAnnually, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.FamiliesAnnually2019, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.FamiliesAnnually2019, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.TeamsAnnually, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.TeamsAnnually, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.TeamsAnnually2020, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.TeamsAnnually2020, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.TeamsAnnually2019, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.TeamsAnnually2019, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.TeamsMonthly, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.TeamsMonthly, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.TeamsMonthly2020, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.TeamsMonthly2020, OrganizationUserType.Owner)]
    [BitAutoData(PlanType.TeamsMonthly2019, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.TeamsMonthly2019, OrganizationUserType.Owner)]
    public async Task ConfirmUserAsync_ToNonFree_WithExistingFreeAdminOrOwner_Succeeds(PlanType planType, OrganizationUserType orgUserType, Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();

        org.PlanType = planType;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;
        orgUser.Type = orgUserType;
        orgUser.AccessSecretsManager = false;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(orgUser.UserId.Value).Returns(1);
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });

        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id);

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
        await sutProvider.GetDependency<IMailService>().Received(1).SendOrganizationConfirmedEmailAsync(org.DisplayName(), user.Email);
        await organizationUserRepository.Received(1).ReplaceManyAsync(Arg.Is<List<OrganizationUser>>(users => users.Contains(orgUser) && users.Count == 1));
    }


    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_AsUser_WithSingleOrgPolicyAppliedFromConfirmingOrg_ThrowsBadRequestException(Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        OrganizationUser orgUserAnotherOrg, [OrganizationUserPolicyDetails(PolicyType.SingleOrg)] OrganizationUserPolicyDetails singleOrgPolicy,
        string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var policyService = sutProvider.GetDependency<IPolicyService>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.Status = OrganizationUserStatusType.Accepted;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = orgUserAnotherOrg.UserId = user.Id;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetManyByManyUsersAsync(default).ReturnsForAnyArgs(new[] { orgUserAnotherOrg });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });
        singleOrgPolicy.OrganizationId = org.Id;
        policyService.GetPoliciesApplicableToUserAsync(user.Id, PolicyType.SingleOrg).Returns(new[] { singleOrgPolicy });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id));
        Assert.Contains("Cannot confirm this member to the organization until they leave or remove all other organizations.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_AsUser_WithSingleOrgPolicyAppliedFromOtherOrg_ThrowsBadRequestException(Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        OrganizationUser orgUserAnotherOrg, [OrganizationUserPolicyDetails(PolicyType.SingleOrg)] OrganizationUserPolicyDetails singleOrgPolicy,
        string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var policyService = sutProvider.GetDependency<IPolicyService>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.Status = OrganizationUserStatusType.Accepted;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = orgUserAnotherOrg.UserId = user.Id;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetManyByManyUsersAsync(default).ReturnsForAnyArgs(new[] { orgUserAnotherOrg });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });
        singleOrgPolicy.OrganizationId = orgUserAnotherOrg.Id;
        policyService.GetPoliciesApplicableToUserAsync(user.Id, PolicyType.SingleOrg).Returns(new[] { singleOrgPolicy });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id));
        Assert.Contains("Cannot confirm this member to the organization because they are in another organization which forbids it.", exception.Message);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task ConfirmUserAsync_AsOwnerOrAdmin_WithSingleOrgPolicy_ExcludedViaUserType_Success(
        OrganizationUserType userType, Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        OrganizationUser orgUserAnotherOrg,
        string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.Type = userType;
        orgUser.Status = OrganizationUserStatusType.Accepted;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = orgUserAnotherOrg.UserId = user.Id;
        orgUser.AccessSecretsManager = true;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetManyByManyUsersAsync(default).ReturnsForAnyArgs(new[] { orgUserAnotherOrg });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });

        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id);

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
        await sutProvider.GetDependency<IMailService>().Received(1).SendOrganizationConfirmedEmailAsync(org.DisplayName(), user.Email, true);
        await organizationUserRepository.Received(1).ReplaceManyAsync(Arg.Is<List<OrganizationUser>>(users => users.Contains(orgUser) && users.Count == 1));
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithTwoFactorPolicyAndTwoFactorDisabled_ThrowsBadRequestException(Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        OrganizationUser orgUserAnotherOrg,
        [OrganizationUserPolicyDetails(PolicyType.TwoFactorAuthentication)] OrganizationUserPolicyDetails twoFactorPolicy,
        string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var policyService = sutProvider.GetDependency<IPolicyService>();
        var twoFactorIsEnabledQuery = sutProvider.GetDependency<ITwoFactorIsEnabledQuery>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = orgUserAnotherOrg.UserId = user.Id;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetManyByManyUsersAsync(default).ReturnsForAnyArgs(new[] { orgUserAnotherOrg });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });
        twoFactorPolicy.OrganizationId = org.Id;
        policyService.GetPoliciesApplicableToUserAsync(user.Id, PolicyType.TwoFactorAuthentication).Returns(new[] { twoFactorPolicy });
        twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user.Id)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>() { (user.Id, false) });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id));
        Assert.Contains("User does not have two-step login enabled.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithTwoFactorPolicyAndTwoFactorEnabled_Succeeds(Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        [OrganizationUserPolicyDetails(PolicyType.TwoFactorAuthentication)] OrganizationUserPolicyDetails twoFactorPolicy,
        string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var policyService = sutProvider.GetDependency<IPolicyService>();
        var twoFactorIsEnabledQuery = sutProvider.GetDependency<ITwoFactorIsEnabledQuery>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });
        twoFactorPolicy.OrganizationId = org.Id;
        policyService.GetPoliciesApplicableToUserAsync(user.Id, PolicyType.TwoFactorAuthentication).Returns(new[] { twoFactorPolicy });
        twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user.Id)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>() { (user.Id, true) });

        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUsersAsync_WithMultipleUsers_ReturnsExpectedMixedResults(Organization org,
        OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser2,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser3,
        OrganizationUser anotherOrgUser, User user1, User user2, User user3,
        [OrganizationUserPolicyDetails(PolicyType.TwoFactorAuthentication)] OrganizationUserPolicyDetails twoFactorPolicy,
        [OrganizationUserPolicyDetails(PolicyType.SingleOrg)] OrganizationUserPolicyDetails singleOrgPolicy,
        string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var policyService = sutProvider.GetDependency<IPolicyService>();
        var twoFactorIsEnabledQuery = sutProvider.GetDependency<ITwoFactorIsEnabledQuery>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser1.OrganizationId = orgUser2.OrganizationId = orgUser3.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser1.UserId = user1.Id;
        orgUser2.UserId = user2.Id;
        orgUser3.UserId = user3.Id;
        anotherOrgUser.UserId = user3.Id;
        var orgUsers = new[] { orgUser1, orgUser2, orgUser3 };
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(orgUsers);
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user1, user2, user3 });
        twoFactorPolicy.OrganizationId = org.Id;
        policyService.GetPoliciesApplicableToUserAsync(Arg.Any<Guid>(), PolicyType.TwoFactorAuthentication).Returns(new[] { twoFactorPolicy });
        twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user1.Id) && ids.Contains(user2.Id) && ids.Contains(user3.Id)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>()
            {
                (user1.Id, true),
                (user2.Id, false),
                (user3.Id, true)
            });
        singleOrgPolicy.OrganizationId = org.Id;
        policyService.GetPoliciesApplicableToUserAsync(user3.Id, PolicyType.SingleOrg)
            .Returns(new[] { singleOrgPolicy });
        organizationUserRepository.GetManyByManyUsersAsync(default)
            .ReturnsForAnyArgs(new[] { orgUser1, orgUser2, orgUser3, anotherOrgUser });

        var keys = orgUsers.ToDictionary(ou => ou.Id, _ => key);
        var result = await sutProvider.Sut.ConfirmUsersAsync(confirmingUser.OrganizationId, keys, confirmingUser.Id);
        Assert.Contains("", result[0].Item2);
        Assert.Contains("User does not have two-step login enabled.", result[1].Item2);
        Assert.Contains("Cannot confirm this member to the organization until they leave or remove all other organizations.", result[2].Item2);
    }
}
