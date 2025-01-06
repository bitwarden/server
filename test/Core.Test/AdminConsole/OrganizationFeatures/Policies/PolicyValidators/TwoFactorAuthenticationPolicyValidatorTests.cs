using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Commands;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

[SutProviderCustomize]
public class TwoFactorAuthenticationPolicyValidatorTests
{
    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_RemovesNonCompliantUsers(
        Organization organization,
        [PolicyUpdate(PolicyType.TwoFactorAuthentication)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.TwoFactorAuthentication, false)] Policy policy,
        SutProvider<TwoFactorAuthenticationPolicyValidator> sutProvider)
    {
        policy.OrganizationId = organization.Id = policyUpdate.OrganizationId;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var orgUserDetailUserInvited = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.User,
            // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
            Email = "user1@test.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = false
        };
        var orgUserDetailUserAcceptedWith2FA = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.User,
            // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
            Email = "user2@test.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = true
        };
        var orgUserDetailUserAcceptedWithout2FA = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.User,
            // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
            Email = "user3@test.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = true
        };
        var orgUserDetailAdmin = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Admin,
            // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
            Email = "admin@test.com",
            Name = "ADMIN",
            UserId = Guid.NewGuid(),
            HasMasterPassword = false
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns(new List<OrganizationUserUserDetails>
            {
                orgUserDetailUserInvited,
                orgUserDetailUserAcceptedWith2FA,
                orgUserDetailUserAcceptedWithout2FA,
                orgUserDetailAdmin
            });

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<OrganizationUserUserDetails>>())
            .Returns(new List<(OrganizationUserUserDetails user, bool hasTwoFactor)>()
            {
                (orgUserDetailUserInvited, false),
                (orgUserDetailUserAcceptedWith2FA, true),
                (orgUserDetailUserAcceptedWithout2FA, false),
                (orgUserDetailAdmin, false),
            });

        var savingUserId = Guid.NewGuid();
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(savingUserId);

        await sutProvider.Sut.OnSaveSideEffectsAsync(policyUpdate, policy);

        var removeOrganizationUserCommand = sutProvider.GetDependency<IRemoveOrganizationUserCommand>();

        await removeOrganizationUserCommand.Received()
            .RemoveUserAsync(policy.OrganizationId, orgUserDetailUserAcceptedWithout2FA.Id, savingUserId);
        await sutProvider.GetDependency<IMailService>().Received()
            .SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(organization.DisplayName(), orgUserDetailUserAcceptedWithout2FA.Email);

        await removeOrganizationUserCommand.DidNotReceive()
            .RemoveUserAsync(policy.OrganizationId, orgUserDetailUserInvited.Id, savingUserId);
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(organization.DisplayName(), orgUserDetailUserInvited.Email);
        await removeOrganizationUserCommand.DidNotReceive()
            .RemoveUserAsync(policy.OrganizationId, orgUserDetailUserAcceptedWith2FA.Id, savingUserId);
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(organization.DisplayName(), orgUserDetailUserAcceptedWith2FA.Email);
        await removeOrganizationUserCommand.DidNotReceive()
            .RemoveUserAsync(policy.OrganizationId, orgUserDetailAdmin.Id, savingUserId);
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(organization.DisplayName(), orgUserDetailAdmin.Email);
    }

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_UsersToBeRemovedDontHaveMasterPasswords_Throws(
        Organization organization,
        [PolicyUpdate(PolicyType.TwoFactorAuthentication)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.TwoFactorAuthentication, false)] Policy policy,
        SutProvider<TwoFactorAuthenticationPolicyValidator> sutProvider)
    {
        policy.OrganizationId = organization.Id = policyUpdate.OrganizationId;

        var orgUserDetailUserWith2FAAndMP = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
            Email = "user1@test.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = true
        };
        var orgUserDetailUserWith2FANoMP = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
            Email = "user2@test.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = false
        };
        var orgUserDetailUserWithout2FA = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
            Email = "user3@test.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = false
        };
        var orgUserDetailAdmin = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Admin,
            // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
            Email = "admin@test.com",
            Name = "ADMIN",
            UserId = Guid.NewGuid(),
            HasMasterPassword = false
        };

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            .Returns(false);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policy.OrganizationId)
            .Returns(new List<OrganizationUserUserDetails>
            {
                orgUserDetailUserWith2FAAndMP,
                orgUserDetailUserWith2FANoMP,
                orgUserDetailUserWithout2FA,
                orgUserDetailAdmin
            });

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Contains(orgUserDetailUserWith2FANoMP.UserId.Value)
                && ids.Contains(orgUserDetailUserWithout2FA.UserId.Value)
                && ids.Contains(orgUserDetailAdmin.UserId.Value)))
            .Returns(new List<(Guid userId, bool hasTwoFactor)>()
            {
                (orgUserDetailUserWith2FANoMP.UserId.Value, true),
                (orgUserDetailUserWithout2FA.UserId.Value, false),
                (orgUserDetailAdmin.UserId.Value, false),
            });

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.OnSaveSideEffectsAsync(policyUpdate, policy));

        Assert.Equal(TwoFactorAuthenticationPolicyValidator.NonCompliantMembersWillLoseAccessMessage, badRequestException.Message);

        await sutProvider.GetDependency<IRemoveOrganizationUserCommand>().DidNotReceiveWithAnyArgs()
            .RemoveUserAsync(organizationId: default, organizationUserId: default, deletingUserId: default);
    }

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_GivenUpdateTo2faPolicy_WhenAccountProvisioningIsDisabled_ThenRevokeUserCommandShouldNotBeCalled(
            Organization organization,
            [PolicyUpdate(PolicyType.TwoFactorAuthentication)]
            PolicyUpdate policyUpdate,
            [Policy(PolicyType.TwoFactorAuthentication, false)]
            Policy policy,
            SutProvider<TwoFactorAuthenticationPolicyValidator> sutProvider)
    {
        policy.OrganizationId = organization.Id = policyUpdate.OrganizationId;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            .Returns(false);

        var orgUserDetailUserAcceptedWithout2Fa = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.User,
            Email = "user3@test.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = true
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns(new List<OrganizationUserUserDetails>
            {
                orgUserDetailUserAcceptedWithout2Fa
            });


        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<OrganizationUserUserDetails>>())
            .Returns(new List<(OrganizationUserUserDetails user, bool hasTwoFactor)>()
            {
                (orgUserDetailUserAcceptedWithout2Fa, false),
            });

        await sutProvider.Sut.OnSaveSideEffectsAsync(policyUpdate, policy);

        await sutProvider.GetDependency<IRevokeNonCompliantOrganizationUserCommand>()
            .DidNotReceive()
            .RevokeNonCompliantOrganizationUsersAsync(Arg.Any<RevokeOrganizationUsersRequest>());
    }

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_GivenUpdateTo2faPolicy_WhenAccountProvisioningIsEnabledAndUserDoesNotHaveMasterPassword_ThenNonCompliantMembersErrorMessageWillReturn(
        Organization organization,
        [PolicyUpdate(PolicyType.TwoFactorAuthentication)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.TwoFactorAuthentication, false)] Policy policy,
        SutProvider<TwoFactorAuthenticationPolicyValidator> sutProvider)
    {
        policy.OrganizationId = organization.Id = policyUpdate.OrganizationId;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            .Returns(true);

        var orgUserDetailUserWithout2Fa = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            Email = "user3@test.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = false
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([orgUserDetailUserWithout2Fa]);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<OrganizationUserUserDetails>>())
            .Returns(new List<(OrganizationUserUserDetails user, bool hasTwoFactor)>()
            {
                (orgUserDetailUserWithout2Fa, false),
            });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.OnSaveSideEffectsAsync(policyUpdate, policy));

        Assert.Equal(TwoFactorAuthenticationPolicyValidator.NonCompliantMembersWillLoseAccessMessage, exception.Message);
    }

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_WhenAccountProvisioningIsEnabledAndUserHasMasterPassword_ThenUserWillBeRevoked(
        Organization organization,
        [PolicyUpdate(PolicyType.TwoFactorAuthentication)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.TwoFactorAuthentication, false)] Policy policy,
        SutProvider<TwoFactorAuthenticationPolicyValidator> sutProvider)
    {
        policy.OrganizationId = organization.Id = policyUpdate.OrganizationId;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            .Returns(true);

        var orgUserDetailUserWithout2Fa = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            Email = "user3@test.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = true
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([orgUserDetailUserWithout2Fa]);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<OrganizationUserUserDetails>>())
            .Returns(new List<(OrganizationUserUserDetails user, bool hasTwoFactor)>()
            {
                (orgUserDetailUserWithout2Fa, true),
            });

        sutProvider.GetDependency<IRevokeNonCompliantOrganizationUserCommand>()
            .RevokeNonCompliantOrganizationUsersAsync(Arg.Any<RevokeOrganizationUsersRequest>())
            .Returns(new CommandResult());

        await sutProvider.Sut.OnSaveSideEffectsAsync(policyUpdate, policy);

        await sutProvider.GetDependency<IRevokeNonCompliantOrganizationUserCommand>()
            .Received(1)
            .RevokeNonCompliantOrganizationUsersAsync(Arg.Any<RevokeOrganizationUsersRequest>());

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationUserRevokedForPolicySingleOrgEmailAsync(organization.DisplayName(),
                "user3@test.com");
    }
}
