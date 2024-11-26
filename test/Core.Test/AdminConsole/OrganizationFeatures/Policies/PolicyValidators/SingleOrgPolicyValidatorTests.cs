using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
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
public class SingleOrgPolicyValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_DisablingPolicy_KeyConnectorEnabled_ValidationError(
        [PolicyUpdate(PolicyType.SingleOrg, false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg)] Policy policy,
        SutProvider<SingleOrgPolicyValidator> sutProvider)
    {
        policy.OrganizationId = policyUpdate.OrganizationId;

        var ssoConfig = new SsoConfig { Enabled = true };
        ssoConfig.SetData(new SsoConfigurationData { MemberDecryptionType = MemberDecryptionType.KeyConnector });

        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(ssoConfig);

        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, policy);
        Assert.Contains("Key Connector is enabled", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DisablingPolicy_KeyConnectorNotEnabled_Success(
        [PolicyUpdate(PolicyType.ResetPassword, false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.ResetPassword)] Policy policy,
        SutProvider<SingleOrgPolicyValidator> sutProvider)
    {
        policy.OrganizationId = policyUpdate.OrganizationId;

        var ssoConfig = new SsoConfig { Enabled = false };

        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(policyUpdate.OrganizationId)
            .Returns(ssoConfig);

        var result = await sutProvider.Sut.ValidateAsync(policyUpdate, policy);
        Assert.True(string.IsNullOrEmpty(result));
    }

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_RevokesNonCompliantUsers(
        [PolicyUpdate(PolicyType.SingleOrg)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg, false)] Policy policy,
        Guid savingUserId,
        Guid nonCompliantUserId,
        Organization organization, SutProvider<SingleOrgPolicyValidator> sutProvider)
    {
        policy.OrganizationId = organization.Id = policyUpdate.OrganizationId;

        var compliantUser1 = new OrganizationUserUserDetails
        {
            OrganizationId = organization.Id,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Confirmed,
            UserId = new Guid(),
            Email = "user1@example.com"
        };

        var compliantUser2 = new OrganizationUserUserDetails
        {
            OrganizationId = organization.Id,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Confirmed,
            UserId = new Guid(),
            Email = "user2@example.com"
        };

        var nonCompliantUser = new OrganizationUserUserDetails
        {
            OrganizationId = organization.Id,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Confirmed,
            UserId = nonCompliantUserId,
            Email = "user3@example.com"
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([compliantUser1, compliantUser2, nonCompliantUser]);

        var otherOrganizationUser = new OrganizationUser
        {
            OrganizationId = new Guid(),
            UserId = nonCompliantUserId,
            Status = OrganizationUserStatusType.Confirmed
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(nonCompliantUserId)))
            .Returns([otherOrganizationUser]);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(savingUserId);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(policyUpdate.OrganizationId).Returns(organization);

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            .Returns(true);

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
                "user3@example.com");
    }

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_RemovesNonCompliantUsers(
        [PolicyUpdate(PolicyType.SingleOrg)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg, false)] Policy policy,
        Guid savingUserId,
        Guid nonCompliantUserId,
        Organization organization, SutProvider<SingleOrgPolicyValidator> sutProvider)
    {
        policy.OrganizationId = organization.Id = policyUpdate.OrganizationId;

        var compliantUser1 = new OrganizationUserUserDetails
        {
            OrganizationId = organization.Id,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Confirmed,
            UserId = new Guid(),
            Email = "user1@example.com"
        };

        var compliantUser2 = new OrganizationUserUserDetails
        {
            OrganizationId = organization.Id,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Confirmed,
            UserId = new Guid(),
            Email = "user2@example.com"
        };

        var nonCompliantUser = new OrganizationUserUserDetails
        {
            OrganizationId = organization.Id,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Confirmed,
            UserId = nonCompliantUserId,
            Email = "user3@example.com"
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([compliantUser1, compliantUser2, nonCompliantUser]);

        var otherOrganizationUser = new OrganizationUser
        {
            OrganizationId = new Guid(),
            UserId = nonCompliantUserId,
            Status = OrganizationUserStatusType.Confirmed
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(nonCompliantUserId)))
            .Returns([otherOrganizationUser]);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(savingUserId);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(policyUpdate.OrganizationId).Returns(organization);

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            .Returns(false);

        sutProvider.GetDependency<IRevokeNonCompliantOrganizationUserCommand>()
            .RevokeNonCompliantOrganizationUsersAsync(Arg.Any<RevokeOrganizationUsersRequest>())
            .Returns(new CommandResult());

        await sutProvider.Sut.OnSaveSideEffectsAsync(policyUpdate, policy);

        await sutProvider.GetDependency<IRemoveOrganizationUserCommand>()
            .Received(1)
            .RemoveUserAsync(policyUpdate.OrganizationId, nonCompliantUser.Id, savingUserId);
        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationUserRemovedForPolicySingleOrgEmailAsync(organization.DisplayName(),
                "user3@example.com");
    }

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_WhenAccountDeprovisioningIsEnabled_ThenUsersAreRevoked(
        [PolicyUpdate(PolicyType.SingleOrg)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.SingleOrg, false)] Policy policy,
        Guid savingUserId,
        Guid nonCompliantUserId,
        Organization organization, SutProvider<SingleOrgPolicyValidator> sutProvider)
    {
        policy.OrganizationId = organization.Id = policyUpdate.OrganizationId;

        var compliantUser1 = new OrganizationUserUserDetails
        {
            OrganizationId = organization.Id,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Confirmed,
            UserId = new Guid(),
            Email = "user1@example.com"
        };

        var compliantUser2 = new OrganizationUserUserDetails
        {
            OrganizationId = organization.Id,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Confirmed,
            UserId = new Guid(),
            Email = "user2@example.com"
        };

        var nonCompliantUser = new OrganizationUserUserDetails
        {
            OrganizationId = organization.Id,
            Type = OrganizationUserType.User,
            Status = OrganizationUserStatusType.Confirmed,
            UserId = nonCompliantUserId,
            Email = "user3@example.com"
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([compliantUser1, compliantUser2, nonCompliantUser]);

        var otherOrganizationUser = new OrganizationUser
        {
            OrganizationId = new Guid(),
            UserId = nonCompliantUserId,
            Status = OrganizationUserStatusType.Confirmed
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(nonCompliantUserId)))
            .Returns([otherOrganizationUser]);

        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(savingUserId);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(policyUpdate.OrganizationId)
            .Returns(organization);

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.AccountDeprovisioning).Returns(true);

        sutProvider.GetDependency<IRevokeNonCompliantOrganizationUserCommand>()
            .RevokeNonCompliantOrganizationUsersAsync(Arg.Any<RevokeOrganizationUsersRequest>())
            .Returns(new CommandResult());

        await sutProvider.Sut.OnSaveSideEffectsAsync(policyUpdate, policy);

        await sutProvider.GetDependency<IRevokeNonCompliantOrganizationUserCommand>()
            .Received()
            .RevokeNonCompliantOrganizationUsersAsync(Arg.Any<RevokeOrganizationUsersRequest>());
    }
}
