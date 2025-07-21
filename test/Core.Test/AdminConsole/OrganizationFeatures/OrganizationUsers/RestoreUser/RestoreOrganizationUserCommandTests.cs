using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser.v1;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.RestoreUser;

[SutProviderCustomize]
public class RestoreOrganizationUserCommandTests
{
    [Theory, BitAutoData]
    public async Task RestoreUser_Success(Organization organization, [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser, SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        RestoreUser_Setup(organization, owner, organizationUser, sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        await sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .RestoreAsync(organizationUser.Id, OrganizationUserStatusType.Invited);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored);
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncOrgKeysAsync(organizationUser.UserId!.Value);
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_WithEventSystemUser_Success(Organization organization, [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser, EventSystemUser eventSystemUser, SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        RestoreUser_Setup(organization, null, organizationUser, sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        await sutProvider.Sut.RestoreUserAsync(organizationUser, eventSystemUser);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .RestoreAsync(organizationUser.Id, OrganizationUserStatusType.Invited);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored, eventSystemUser);
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncOrgKeysAsync(organizationUser.UserId!.Value);
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_RestoreThemselves_Fails(Organization organization, [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser, SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        organizationUser.UserId = owner.Id;
        RestoreUser_Setup(organization, owner, organizationUser, sutProvider);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id));

        Assert.Contains("you cannot restore yourself", exception.Message.ToLowerInvariant());

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .RestoreAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType>());
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<EventSystemUser>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushSyncOrgKeysAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task RestoreUser_AdminRestoreOwner_Fails(OrganizationUserType restoringUserType,
        Organization organization, [OrganizationUser(OrganizationUserStatusType.Confirmed)] OrganizationUser restoringUser,
        [OrganizationUser(OrganizationUserStatusType.Revoked, OrganizationUserType.Owner)] OrganizationUser organizationUser, SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        restoringUser.Type = restoringUserType;
        RestoreUser_Setup(organization, restoringUser, organizationUser, sutProvider);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreUserAsync(organizationUser, restoringUser.Id));

        Assert.Contains("only owners can restore other owners", exception.Message.ToLowerInvariant());

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .RestoreAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType>());
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<EventSystemUser>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushSyncOrgKeysAsync(Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public async Task RestoreUser_WithStatusOtherThanRevoked_Fails(OrganizationUserStatusType userStatus, Organization organization, [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser] OrganizationUser organizationUser, SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        organizationUser.Status = userStatus;
        RestoreUser_Setup(organization, owner, organizationUser, sutProvider);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id));

        Assert.Contains("already active", exception.Message.ToLowerInvariant());

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .RestoreAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType>());
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<EventSystemUser>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushSyncOrgKeysAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_WithOtherOrganizationSingleOrgPolicyEnabled_Fails(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        organizationUser.Email = null; // this is required to mock that the user as had already been confirmed before the revoke
        RestoreUser_Setup(organization, owner, organizationUser, sutProvider);

        sutProvider.GetDependency<IPolicyService>()
            .AnyPoliciesApplicableToUserAsync(organizationUser.UserId.Value, PolicyType.SingleOrg, Arg.Any<OrganizationUserStatusType>())
            .Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        var user = new User();
        user.Email = "test@bitwarden.com";
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(organizationUser.UserId.Value).Returns(user);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id));

        Assert.Contains("test@bitwarden.com belongs to an organization that doesn't allow them to join multiple organizations", exception.Message.ToLowerInvariant());

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .RestoreAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType>());
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<EventSystemUser>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushSyncOrgKeysAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_With2FAPolicyEnabled_WithoutUser2FAConfigured_Fails(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        organizationUser.Email = null;

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.UserId.Value)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>() { (organizationUser.UserId.Value, false) });
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        RestoreUser_Setup(organization, owner, organizationUser, sutProvider);

        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(organizationUser.UserId.Value, PolicyType.TwoFactorAuthentication, Arg.Any<OrganizationUserStatusType>())
            .Returns(new[] { new OrganizationUserPolicyDetails { OrganizationId = organizationUser.OrganizationId, PolicyType = PolicyType.TwoFactorAuthentication } });

        var user = new User();
        user.Email = "test@bitwarden.com";
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(organizationUser.UserId.Value).Returns(user);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id));

        Assert.Contains("test@bitwarden.com is not compliant with the two-step login policy", exception.Message.ToLowerInvariant());

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .RestoreAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType>());
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<EventSystemUser>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushSyncOrgKeysAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_WithPolicyRequirementsEnabled_With2FAPolicyEnabled_WithoutUser2FAConfigured_Fails(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        organizationUser.Email = null;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(true);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.UserId.Value)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>() { (organizationUser.UserId.Value, false) });

        RestoreUser_Setup(organization, owner, organizationUser, sutProvider);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(organizationUser.UserId.Value)
            .Returns(new RequireTwoFactorPolicyRequirement(
            [
                new PolicyDetails
                {
                    OrganizationId = organizationUser.OrganizationId,
                    OrganizationUserStatus = OrganizationUserStatusType.Revoked,
                    PolicyType = PolicyType.TwoFactorAuthentication
                }
            ]));

        var user = new User();
        user.Email = "test@bitwarden.com";
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(organizationUser.UserId.Value).Returns(user);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id));

        Assert.Contains("test@bitwarden.com is not compliant with the two-step login policy", exception.Message.ToLowerInvariant());

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .RestoreAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType>());
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<EventSystemUser>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushSyncOrgKeysAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_With2FAPolicyEnabled_WithUser2FAConfigured_Success(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        organizationUser.Email = null; // this is required to mock that the user as had already been confirmed before the revoke
        RestoreUser_Setup(organization, owner, organizationUser, sutProvider);

        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(organizationUser.UserId.Value, PolicyType.TwoFactorAuthentication, Arg.Any<OrganizationUserStatusType>())
            .Returns(new[] { new OrganizationUserPolicyDetails { OrganizationId = organizationUser.OrganizationId, PolicyType = PolicyType.TwoFactorAuthentication } });
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.UserId.Value)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>() { (organizationUser.UserId.Value, true) });
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        await sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .RestoreAsync(organizationUser.Id, OrganizationUserStatusType.Confirmed);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored);
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_WithPolicyRequirementsEnabled_With2FAPolicyEnabled_WithUser2FAConfigured_Success(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        organizationUser.Email = null; // this is required to mock that the user as had already been confirmed before the revoke

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(true);

        RestoreUser_Setup(organization, owner, organizationUser, sutProvider);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.UserId.Value)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>() { (organizationUser.UserId.Value, true) });
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(organizationUser.UserId.Value)
            .Returns(new RequireTwoFactorPolicyRequirement(
            [
                new PolicyDetails
                {
                    OrganizationId = organizationUser.OrganizationId,
                    OrganizationUserStatus = OrganizationUserStatusType.Revoked,
                    PolicyType = PolicyType.TwoFactorAuthentication
                }
            ]));

        await sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .RestoreAsync(organizationUser.Id, OrganizationUserStatusType.Confirmed);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored);
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_WithSingleOrgPolicyEnabled_Fails(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser secondOrganizationUser,
        SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        organizationUser.Email = null; // this is required to mock that the user as had already been confirmed before the revoke
        secondOrganizationUser.UserId = organizationUser.UserId;
        RestoreUser_Setup(organization, owner, organizationUser, sutProvider);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(organizationUser.UserId.Value)
            .Returns(new[] { organizationUser, secondOrganizationUser });
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.UserId.Value)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)> { (organizationUser.UserId.Value, true) });
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(organizationUser.UserId.Value, PolicyType.SingleOrg, Arg.Any<OrganizationUserStatusType>())
            .Returns(new[]
            {
                new OrganizationUserPolicyDetails { OrganizationId = organizationUser.OrganizationId, PolicyType = PolicyType.SingleOrg, OrganizationUserStatus = OrganizationUserStatusType.Revoked }
            });

        var user = new User();
        user.Email = "test@bitwarden.com";
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(organizationUser.UserId.Value).Returns(user);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id));

        Assert.Contains("test@bitwarden.com is not compliant with the single organization policy", exception.Message.ToLowerInvariant());

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .RestoreAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType>());
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<EventSystemUser>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushSyncOrgKeysAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_WithSingleOrgPolicyEnabled_And_2FA_Policy_Fails(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser secondOrganizationUser,
        SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        organizationUser.Email = null; // this is required to mock that the user as had already been confirmed before the revoke
        secondOrganizationUser.UserId = organizationUser.UserId;
        RestoreUser_Setup(organization, owner, organizationUser, sutProvider);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(organizationUser.UserId.Value)
            .Returns(new[] { organizationUser, secondOrganizationUser });
        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(organizationUser.UserId.Value, PolicyType.SingleOrg, Arg.Any<OrganizationUserStatusType>())
            .Returns(new[]
            {
                new OrganizationUserPolicyDetails { OrganizationId = organizationUser.OrganizationId, PolicyType = PolicyType.SingleOrg, OrganizationUserStatus = OrganizationUserStatusType.Revoked }
            });
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(organizationUser.UserId.Value, PolicyType.TwoFactorAuthentication, Arg.Any<OrganizationUserStatusType>())
            .Returns([
                new OrganizationUserPolicyDetails { OrganizationId = organizationUser.OrganizationId, PolicyType = PolicyType.TwoFactorAuthentication, OrganizationUserStatus = OrganizationUserStatusType.Revoked }
            ]);

        var user = new User { Email = "test@bitwarden.com" };
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(organizationUser.UserId.Value).Returns(user);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id));

        Assert.Contains("test@bitwarden.com is not compliant with the single organization and two-step login policy", exception.Message.ToLowerInvariant());

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .RestoreAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType>());
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<EventSystemUser>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushSyncOrgKeysAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_WithPolicyRequirementsEnabled_WithSingleOrgPolicyEnabled_And_2FA_Policy_Fails(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser secondOrganizationUser,
        SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        organizationUser.Email = null; // this is required to mock that the user as had already been confirmed before the revoke
        secondOrganizationUser.UserId = organizationUser.UserId;
        RestoreUser_Setup(organization, owner, organizationUser, sutProvider);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(organizationUser.UserId.Value)
            .Returns(new[] { organizationUser, secondOrganizationUser });
        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(organizationUser.UserId.Value, PolicyType.SingleOrg, Arg.Any<OrganizationUserStatusType>())
            .Returns(new[]
            {
                new OrganizationUserPolicyDetails { OrganizationId = organizationUser.OrganizationId, PolicyType = PolicyType.SingleOrg, OrganizationUserStatus = OrganizationUserStatusType.Revoked }
            });

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(organizationUser.UserId.Value)
            .Returns(new RequireTwoFactorPolicyRequirement(
            [
                new PolicyDetails
                {
                    OrganizationId = organizationUser.OrganizationId,
                    OrganizationUserStatus = OrganizationUserStatusType.Revoked,
                    PolicyType = PolicyType.TwoFactorAuthentication
                }
            ]));
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        var user = new User { Email = "test@bitwarden.com" };
        sutProvider.GetDependency<IUserRepository>().GetByIdAsync(organizationUser.UserId.Value).Returns(user);

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id));

        Assert.Contains("test@bitwarden.com is not compliant with the single organization and two-step login policy", exception.Message.ToLowerInvariant());

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .RestoreAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType>());
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(Arg.Any<OrganizationUser>(), Arg.Any<EventType>(), Arg.Any<EventSystemUser>());
        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushSyncOrgKeysAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_vNext_With2FAPolicyEnabled_WithUser2FAConfigured_Success(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        organizationUser.Email = null; // this is required to mock that the user as had already been confirmed before the revoke
        RestoreUser_Setup(organization, owner, organizationUser, sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(organizationUser.UserId.Value, PolicyType.TwoFactorAuthentication, Arg.Any<OrganizationUserStatusType>())
            .Returns([new OrganizationUserPolicyDetails { OrganizationId = organizationUser.OrganizationId, PolicyType = PolicyType.TwoFactorAuthentication }
            ]);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.UserId.Value)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)> { (organizationUser.UserId.Value, true) });

        await sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .RestoreAsync(organizationUser.Id, OrganizationUserStatusType.Confirmed);
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Restored);
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_WhenUserOwningAnotherFreeOrganization_ThenRestoreUserFails(
        Organization organization,
        Organization otherOrganization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked, OrganizationUserType.Owner)] OrganizationUser organizationUser,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser orgUserOwnerFromDifferentOrg,
        SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        organization.PlanType = PlanType.Free;
        organizationUser.Email = null; // this is required to mock that the user as had already been confirmed before the revoke

        orgUserOwnerFromDifferentOrg.UserId = organizationUser.UserId;
        otherOrganization.Id = orgUserOwnerFromDifferentOrg.OrganizationId;
        otherOrganization.PlanType = PlanType.Free;

        RestoreUser_Setup(organization, owner, organizationUser, sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(organizationUser.UserId.Value)
            .Returns([orgUserOwnerFromDifferentOrg]);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyByUserIdAsync(organizationUser.UserId.Value)
            .Returns([otherOrganization]);

        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(organizationUser.UserId.Value, PolicyType.TwoFactorAuthentication, Arg.Any<OrganizationUserStatusType>())
            .Returns([new OrganizationUserPolicyDetails { OrganizationId = organizationUser.OrganizationId, PolicyType = PolicyType.TwoFactorAuthentication }
            ]);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.UserId.Value)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)> { (organizationUser.UserId.Value, true) });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id));

        Assert.Equal("User is an owner/admin of another free organization. Please have them upgrade to a paid plan to restore their account.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_WhenUserOwningAnotherFreeOrganizationAndIsOnlyAUserInCurrentOrg_ThenUserShouldBeRestored(
        Organization organization,
        Organization otherOrganization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser organizationUser,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser orgUserOwnerFromDifferentOrg,
        SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        organization.PlanType = PlanType.Free;
        organizationUser.Email = null; // this is required to mock that the user as had already been confirmed before the revoke

        orgUserOwnerFromDifferentOrg.UserId = organizationUser.UserId;
        otherOrganization.Id = orgUserOwnerFromDifferentOrg.OrganizationId;
        otherOrganization.PlanType = PlanType.Free;

        RestoreUser_Setup(organization, owner, organizationUser, sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        organizationUserRepository
            .GetManyByUserAsync(organizationUser.UserId.Value)
            .Returns([orgUserOwnerFromDifferentOrg]);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyByUserIdAsync(organizationUser.UserId.Value)
            .Returns([otherOrganization]);

        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(organizationUser.UserId.Value, PolicyType.TwoFactorAuthentication,
                Arg.Any<OrganizationUserStatusType>())
            .Returns([
                new OrganizationUserPolicyDetails
                {
                    OrganizationId = organizationUser.OrganizationId,
                    PolicyType = PolicyType.TwoFactorAuthentication
                }
            ]);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.UserId.Value)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)> { (organizationUser.UserId.Value, true) });

        await sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id);

        await organizationUserRepository
            .Received(1)
            .RestoreAsync(organizationUser.Id,
                Arg.Is<OrganizationUserStatusType>(x => x != OrganizationUserStatusType.Revoked));
    }

    [Theory, BitAutoData]
    public async Task RestoreUser_WhenUserOwningAnotherFreeOrganizationAndCurrentOrgIsNotFree_ThenUserShouldBeRestored(
        Organization organization,
        Organization otherOrganization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked, OrganizationUserType.Owner)] OrganizationUser organizationUser,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser orgUserOwnerFromDifferentOrg,
        SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        organization.PlanType = PlanType.EnterpriseAnnually2023;

        organizationUser.Email = null; // this is required to mock that the user as had already been confirmed before the revoke

        orgUserOwnerFromDifferentOrg.UserId = organizationUser.UserId;
        otherOrganization.Id = orgUserOwnerFromDifferentOrg.OrganizationId;
        otherOrganization.PlanType = PlanType.Free;

        RestoreUser_Setup(organization, owner, organizationUser, sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        organizationUserRepository
            .GetManyByUserAsync(organizationUser.UserId.Value)
            .Returns([orgUserOwnerFromDifferentOrg]);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyByUserIdAsync(organizationUser.UserId.Value)
            .Returns([otherOrganization]);

        sutProvider.GetDependency<IPolicyService>()
            .GetPoliciesApplicableToUserAsync(organizationUser.UserId.Value, PolicyType.TwoFactorAuthentication,
                Arg.Any<OrganizationUserStatusType>())
            .Returns([
                new OrganizationUserPolicyDetails
                {
                    OrganizationId = organizationUser.OrganizationId,
                    PolicyType = PolicyType.TwoFactorAuthentication
                }
            ]);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(i => i.Contains(organizationUser.UserId.Value)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)> { (organizationUser.UserId.Value, true) });

        await sutProvider.Sut.RestoreUserAsync(organizationUser, owner.Id);

        await organizationUserRepository
            .Received(1)
            .RestoreAsync(organizationUser.Id,
                Arg.Is<OrganizationUserStatusType>(x => x != OrganizationUserStatusType.Revoked));
    }

    [Theory, BitAutoData]
    public async Task RestoreUsers_Success(Organization organization,
    [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
    [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser orgUser1,
    [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser orgUser2,
    SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        // Arrange
        RestoreUser_Setup(organization, owner, orgUser1, sutProvider);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var eventService = sutProvider.GetDependency<IEventService>();
        var twoFactorIsEnabledQuery = sutProvider.GetDependency<ITwoFactorIsEnabledQuery>();
        var userService = Substitute.For<IUserService>();

        orgUser1.Email = orgUser2.Email = null; // Mock that users were previously confirmed
        orgUser1.OrganizationId = orgUser2.OrganizationId = organization.Id;
        organizationUserRepository
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUser1.Id) && ids.Contains(orgUser2.Id)))
            .Returns([orgUser1, orgUser2]);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        twoFactorIsEnabledQuery
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUser1.UserId!.Value) && ids.Contains(orgUser2.UserId!.Value)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>
            {
                (orgUser1.UserId!.Value, true),
                (orgUser2.UserId!.Value, false)
            });

        // Act
        var result = await sutProvider.Sut.RestoreUsersAsync(organization.Id, new[] { orgUser1.Id, orgUser2.Id }, owner.Id, userService);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Empty(r.Item2)); // No error messages
        await organizationUserRepository
            .Received(1)
            .RestoreAsync(orgUser1.Id, OrganizationUserStatusType.Confirmed);
        await organizationUserRepository
            .Received(1)
            .RestoreAsync(orgUser2.Id, OrganizationUserStatusType.Confirmed);
        await eventService.Received(1)
            .LogOrganizationUserEventAsync(orgUser1, EventType.OrganizationUser_Restored);
        await eventService.Received(1)
            .LogOrganizationUserEventAsync(orgUser2, EventType.OrganizationUser_Restored);
    }

    [Theory, BitAutoData]
    public async Task RestoreUsers_With2FAPolicy_BlocksNonCompliantUser(Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser orgUser2,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser orgUser3,
        SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        // Arrange
        RestoreUser_Setup(organization, owner, orgUser1, sutProvider);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var policyService = sutProvider.GetDependency<IPolicyService>();
        var userService = Substitute.For<IUserService>();

        orgUser1.Email = orgUser2.Email = null;
        orgUser3.UserId = null;
        orgUser3.Key = null;
        orgUser1.OrganizationId = orgUser2.OrganizationId = orgUser3.OrganizationId = organization.Id;
        organizationUserRepository
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUser1.Id) && ids.Contains(orgUser2.Id) && ids.Contains(orgUser3.Id)))
            .Returns(new[] { orgUser1, orgUser2, orgUser3 });
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        userRepository.GetByIdAsync(orgUser2.UserId!.Value).Returns(new User { Email = "test@example.com" });

        // Setup 2FA policy
        policyService.GetPoliciesApplicableToUserAsync(Arg.Any<Guid>(), PolicyType.TwoFactorAuthentication, Arg.Any<OrganizationUserStatusType>())
            .Returns([new OrganizationUserPolicyDetails { OrganizationId = organization.Id, PolicyType = PolicyType.TwoFactorAuthentication }]);

        // User1 has 2FA, User2 doesn't
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUser1.UserId!.Value) && ids.Contains(orgUser2.UserId!.Value)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>
            {
                (orgUser1.UserId!.Value, true),
                (orgUser2.UserId!.Value, false)
            });

        // Act
        var result = await sutProvider.Sut.RestoreUsersAsync(organization.Id, [orgUser1.Id, orgUser2.Id, orgUser3.Id], owner.Id, userService);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Empty(result[0].Item2); // First user should succeed
        Assert.Contains("two-step login", result[1].Item2); // Second user should fail
        Assert.Empty(result[2].Item2); // Third user should succeed
        await organizationUserRepository
            .Received(1)
            .RestoreAsync(orgUser1.Id, OrganizationUserStatusType.Confirmed);
        await organizationUserRepository
            .DidNotReceive()
            .RestoreAsync(orgUser2.Id, Arg.Any<OrganizationUserStatusType>());
        await organizationUserRepository
            .Received(1)
            .RestoreAsync(orgUser3.Id, OrganizationUserStatusType.Invited);
    }

    [Theory, BitAutoData]
    public async Task RestoreUsers_WithPolicyRequirementsEnabled_With2FAPolicy_BlocksNonCompliantUser(Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser orgUser2,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser orgUser3,
        SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        // Arrange
        RestoreUser_Setup(organization, owner, orgUser1, sutProvider);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PolicyRequirements)
            .Returns(true);

        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var policyService = sutProvider.GetDependency<IPolicyService>();
        var userService = Substitute.For<IUserService>();

        orgUser1.Email = orgUser2.Email = null;
        orgUser3.UserId = null;
        orgUser3.Key = null;
        orgUser1.OrganizationId = orgUser2.OrganizationId = orgUser3.OrganizationId = organization.Id;
        organizationUserRepository
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUser1.Id) && ids.Contains(orgUser2.Id) && ids.Contains(orgUser3.Id)))
            .Returns(new[] { orgUser1, orgUser2, orgUser3 });

        userRepository.GetByIdAsync(orgUser2.UserId!.Value).Returns(new User { Email = "test@example.com" });

        // Setup 2FA policy
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireTwoFactorPolicyRequirement(
            [
                new PolicyDetails
                {
                    OrganizationId = organization.Id,
                    OrganizationUserStatus = OrganizationUserStatusType.Revoked,
                    PolicyType = PolicyType.TwoFactorAuthentication
                }
            ]));

        // User1 has 2FA, User2 doesn't
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUser1.UserId!.Value) && ids.Contains(orgUser2.UserId!.Value)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>
            {
                (orgUser1.UserId!.Value, true),
                (orgUser2.UserId!.Value, false)
            });

        // Act
        var result = await sutProvider.Sut.RestoreUsersAsync(organization.Id, [orgUser1.Id, orgUser2.Id, orgUser3.Id], owner.Id, userService);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Empty(result[0].Item2); // First user should succeed
        Assert.Contains("two-step login", result[1].Item2); // Second user should fail
        Assert.Empty(result[2].Item2); // Third user should succeed
        await organizationUserRepository
            .Received(1)
            .RestoreAsync(orgUser1.Id, OrganizationUserStatusType.Confirmed);
        await organizationUserRepository
            .DidNotReceive()
            .RestoreAsync(orgUser2.Id, Arg.Any<OrganizationUserStatusType>());
        await organizationUserRepository
            .Received(1)
            .RestoreAsync(orgUser3.Id, OrganizationUserStatusType.Invited);
    }

    [Theory, BitAutoData]
    public async Task RestoreUsers_UserOwnsAnotherFreeOrganization_BlocksOwnerUserFromBeingRestored(Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked, OrganizationUserType.Owner)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser orgUser2,
        [OrganizationUser(OrganizationUserStatusType.Revoked)] OrganizationUser orgUser3,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser orgUserFromOtherOrg,
        Organization otherOrganization,
        SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        // Arrange
        RestoreUser_Setup(organization, owner, orgUser1, sutProvider);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var policyService = sutProvider.GetDependency<IPolicyService>();
        var userService = Substitute.For<IUserService>();

        orgUser1.Email = orgUser2.Email = null;
        orgUser3.UserId = null;
        orgUser3.Key = null;
        orgUser1.OrganizationId = orgUser2.OrganizationId = orgUser3.OrganizationId = organization.Id;

        orgUserFromOtherOrg.UserId = orgUser1.UserId;
        otherOrganization.Id = orgUserFromOtherOrg.OrganizationId;
        otherOrganization.PlanType = PlanType.Free;

        organizationUserRepository
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUser1.Id) && ids.Contains(orgUser2.Id) && ids.Contains(orgUser3.Id)))
            .Returns([orgUser1, orgUser2, orgUser3]);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        userRepository.GetByIdAsync(orgUser2.UserId!.Value).Returns(new User { Email = "test@example.com" });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([orgUserFromOtherOrg]);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyByIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUserFromOtherOrg.OrganizationId)))
            .Returns([otherOrganization]);


        // Setup 2FA policy
        policyService.GetPoliciesApplicableToUserAsync(Arg.Any<Guid>(), PolicyType.TwoFactorAuthentication, Arg.Any<OrganizationUserStatusType>())
            .Returns([new OrganizationUserPolicyDetails { OrganizationId = organization.Id, PolicyType = PolicyType.TwoFactorAuthentication }]);

        // User1 has 2FA, User2 doesn't
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUser1.UserId!.Value) && ids.Contains(orgUser2.UserId!.Value)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>
            {
                (orgUser1.UserId!.Value, true),
                (orgUser2.UserId!.Value, false)
            });

        // Act
        var result = await sutProvider.Sut.RestoreUsersAsync(organization.Id, [orgUser1.Id, orgUser2.Id, orgUser3.Id], owner.Id, userService);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("owner", result[0].Item2); // Owner should fail
        await organizationUserRepository
            .DidNotReceive()
            .RestoreAsync(orgUser1.Id, OrganizationUserStatusType.Confirmed);
    }

    [Theory, BitAutoData]
    public async Task RestoreUsers_UserOwnsAnotherFreeOrganizationButReactivatingOrgIsPaid_RestoresUser(Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked, OrganizationUserType.Owner)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser orgUserFromOtherOrg,
        Organization otherOrganization,
        SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually2023;

        RestoreUser_Setup(organization, owner, orgUser1, sutProvider);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var policyService = sutProvider.GetDependency<IPolicyService>();
        var userService = Substitute.For<IUserService>();

        orgUser1.OrganizationId = organization.Id;

        orgUserFromOtherOrg.UserId = orgUser1.UserId;

        otherOrganization.Id = orgUserFromOtherOrg.OrganizationId;
        otherOrganization.PlanType = PlanType.Free;

        organizationUserRepository
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUser1.Id)))
            .Returns([orgUser1]);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        organizationUserRepository
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([orgUserFromOtherOrg]);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetManyByIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUserFromOtherOrg.OrganizationId)))
            .Returns([otherOrganization]);


        // Setup 2FA policy
        policyService.GetPoliciesApplicableToUserAsync(Arg.Any<Guid>(), PolicyType.TwoFactorAuthentication, Arg.Any<OrganizationUserStatusType>())
            .Returns([new OrganizationUserPolicyDetails { OrganizationId = organization.Id, PolicyType = PolicyType.TwoFactorAuthentication }]);

        // User1 has 2FA, User2 doesn't
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUser1.UserId!.Value)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>
            {
                (orgUser1.UserId!.Value, true)
            });

        // Act
        var result = await sutProvider.Sut.RestoreUsersAsync(organization.Id, [orgUser1.Id], owner.Id, userService);

        // Assert
        Assert.Single(result);
        Assert.Equal(string.Empty, result[0].Item2);
        await organizationUserRepository
            .Received(1)
            .RestoreAsync(orgUser1.Id, Arg.Is<OrganizationUserStatusType>(x => x != OrganizationUserStatusType.Revoked));
    }

    [Theory]
    [BitAutoData]
    public async Task RestoreUsers_UserOwnsAnotherOrganizationButIsOnlyUserOfCurrentOrganization_UserShouldBeRestored(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser owner,
        [OrganizationUser(OrganizationUserStatusType.Revoked, OrganizationUserType.User)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser orgUserFromOtherOrg,
        Organization otherOrganization,
        SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.Free;

        RestoreUser_Setup(organization, owner, orgUser1, sutProvider);
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var userService = Substitute.For<IUserService>();

        orgUser1.OrganizationId = organization.Id;

        orgUserFromOtherOrg.UserId = orgUser1.UserId;

        otherOrganization.Id = orgUserFromOtherOrg.OrganizationId;
        otherOrganization.PlanType = PlanType.Free;

        organizationUserRepository
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUser1.Id)))
            .Returns([orgUser1]);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
            {
                Sponsored = 0,
                Users = 1
            });
        organizationUserRepository
            .GetManyByManyUsersAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([orgUserFromOtherOrg]);

        sutProvider.GetDependency<IPolicyService>().GetPoliciesApplicableToUserAsync(Arg.Any<Guid>(), PolicyType.TwoFactorAuthentication, Arg.Any<OrganizationUserStatusType>())
            .Returns([new OrganizationUserPolicyDetails { OrganizationId = organization.Id, PolicyType = PolicyType.TwoFactorAuthentication }]);

        // Act
        var result = await sutProvider.Sut.RestoreUsersAsync(organization.Id, [orgUser1.Id], owner.Id, userService);

        Assert.Single(result);
        Assert.Equal(string.Empty, result[0].Item2);
        await organizationUserRepository
            .Received(1)
            .RestoreAsync(orgUser1.Id, Arg.Is<OrganizationUserStatusType>(x => x != OrganizationUserStatusType.Revoked));
    }

    private static void RestoreUser_Setup(
        Organization organization,
        OrganizationUser? requestingOrganizationUser,
        OrganizationUser targetOrganizationUser,
        SutProvider<RestoreOrganizationUserCommand> sutProvider)
    {
        if (requestingOrganizationUser != null)
        {
            requestingOrganizationUser.OrganizationId = organization.Id;
        }
        targetOrganizationUser.OrganizationId = organization.Id;

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        organizationRepository.GetOccupiedSeatCountByOrganizationIdAsync(organization.Id).Returns(new OrganizationSeatCounts
        {
            Sponsored = 0,
            Users = 1
        });

        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(organization.Id).Returns(requestingOrganizationUser != null && requestingOrganizationUser.Type is OrganizationUserType.Owner);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organization.Id).Returns(requestingOrganizationUser != null && (requestingOrganizationUser.Type is OrganizationUserType.Owner or OrganizationUserType.Admin));
    }
}
