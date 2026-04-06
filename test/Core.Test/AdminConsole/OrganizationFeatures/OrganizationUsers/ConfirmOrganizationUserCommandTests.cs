using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationConfirmation;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Auth.UserFeatures.EmergencyAccess.Interfaces;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

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
    [BitAutoData(PlanType.FamiliesAnnually2025, OrganizationUserType.Admin)]
    [BitAutoData(PlanType.FamiliesAnnually2025, OrganizationUserType.Owner)]
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

        var device = new Device() { Id = Guid.NewGuid(), UserId = user.Id, PushToken = "pushToken", Identifier = "identifier" };
        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns([device]);

        org.PlanType = planType;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;
        orgUser.Type = orgUserType;
        orgUser.AccessSecretsManager = false;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetCountByFreeOrganizationAdminUserAsync(orgUser.UserId.Value).Returns(1);
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id);

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
        await sutProvider.GetDependency<ISendOrganizationConfirmationCommand>().Received(1).SendConfirmationAsync(org, user.Email, orgUser.AccessSecretsManager);
        await organizationUserRepository.Received(1).ReplaceManyAsync(Arg.Is<List<OrganizationUser>>(users => users.Contains(orgUser) && users.Count == 1));
        await sutProvider.GetDependency<IPushRegistrationService>()
            .Received(1)
            .DeleteUserRegistrationOrganizationAsync(
                Arg.Is<IEnumerable<string>>(ids => ids.Contains(device.Id.ToString()) && ids.Count() == 1),
                org.Id.ToString());
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushSyncOrgKeysAsync(user.Id);
    }


    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithSingleOrgPolicyFromConfirmingOrg_ThrowsBadRequest(
        Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        OrganizationUser orgUserAnotherOrg,
        string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.Status = OrganizationUserStatusType.Accepted;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = orgUserAnotherOrg.UserId = user.Id;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetManyByManyUsersAsync(default).ReturnsForAnyArgs(new[] { orgUser, orgUserAnotherOrg });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });

        // 2FA check passes (no 2FA policy)
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user.Id)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>() { (user.Id, false) });
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id)
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        // Confirming org has SingleOrg policy, user is a regular User (not exempt)
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(SingleOrganizationPolicyRequirementTestFactory.EnabledForTargetOrganization(org.Id));

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id));
        Assert.Contains($"{user.Email} cannot be confirmed until they leave or remove all other organizations.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithSingleOrgPolicyFromOtherOrg_ThrowsBadRequest(
        Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        OrganizationUser orgUserAnotherOrg,
        string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.Status = OrganizationUserStatusType.Accepted;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = orgUserAnotherOrg.UserId = user.Id;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetManyByManyUsersAsync(default).ReturnsForAnyArgs(new[] { orgUser, orgUserAnotherOrg });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });

        // 2FA check passes (no 2FA policy)
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user.Id)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>() { (user.Id, false) });
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id)
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        // Other org has SingleOrg policy (not the confirming org)
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(SingleOrganizationPolicyRequirementTestFactory.EnabledForAnotherOrganization());

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id));
        Assert.Contains($"{user.Email} cannot be confirmed because they are in another organization which forbids it.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_NoSingleOrgPolicy_Succeeds(
        Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetManyByManyUsersAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });

        // No SingleOrg policy
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(SingleOrganizationPolicyRequirementTestFactory.NoSinglePolicyOrganizationsForUser());

        // No 2FA policy either
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(user.Id)
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user.Id)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>() { (user.Id, false) });

        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id);

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithTwoFactorRequired_ThrowsBadRequestException(
        Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var policyRequirementQuery = sutProvider.GetDependency<IPolicyRequirementQuery>();
        var twoFactorIsEnabledQuery = sutProvider.GetDependency<ITwoFactorIsEnabledQuery>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });
        policyRequirementQuery.GetAsync<RequireTwoFactorPolicyRequirement>(user.Id)
            .Returns(new RequireTwoFactorPolicyRequirement(
            [
                new PolicyDetails
                {
                    OrganizationId = org.Id,
                    OrganizationUserStatus = OrganizationUserStatusType.Accepted,
                    PolicyType = PolicyType.TwoFactorAuthentication
                }
            ]));
        twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user.Id)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>() { (user.Id, false) });

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, "key", confirmingUser.Id));
        Assert.Contains("User does not have two-step login enabled.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithTwoFactorNotRequired_Succeeds(
        Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var policyRequirementQuery = sutProvider.GetDependency<IPolicyRequirementQuery>();
        var twoFactorIsEnabledQuery = sutProvider.GetDependency<ITwoFactorIsEnabledQuery>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetManyByManyUsersAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });
        policyRequirementQuery.GetAsync<RequireTwoFactorPolicyRequirement>(user.Id)
            .Returns(new RequireTwoFactorPolicyRequirement(
            [
                new PolicyDetails
                {
                    OrganizationId = Guid.NewGuid(),
                    OrganizationUserStatus = OrganizationUserStatusType.Invited,
                    PolicyType = PolicyType.TwoFactorAuthentication,
                }
            ]));
        policyRequirementQuery.GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));
        twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user.Id)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>() { (user.Id, false) });

        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, "key", confirmingUser.Id);

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
        await sutProvider.GetDependency<ISendOrganizationConfirmationCommand>().Received(1).SendConfirmationAsync(org, user.Email, orgUser.AccessSecretsManager);
        await organizationUserRepository.Received(1).ReplaceManyAsync(Arg.Is<List<OrganizationUser>>(users => users.Contains(orgUser) && users.Count == 1));
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithTwoFactorEnabled_Succeeds(
        Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var userRepository = sutProvider.GetDependency<IUserRepository>();
        var policyRequirementQuery = sutProvider.GetDependency<IPolicyRequirementQuery>();
        var twoFactorIsEnabledQuery = sutProvider.GetDependency<ITwoFactorIsEnabledQuery>();

        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;
        organizationUserRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationUserRepository.GetManyByManyUsersAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        organizationRepository.GetByIdAsync(org.Id).Returns(org);
        userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] { user });
        policyRequirementQuery.GetAsync<RequireTwoFactorPolicyRequirement>(user.Id)
            .Returns(new RequireTwoFactorPolicyRequirement(
            [
                new PolicyDetails
                {
                    OrganizationId = org.Id,
                    OrganizationUserStatus = OrganizationUserStatusType.Accepted,
                    PolicyType = PolicyType.TwoFactorAuthentication
                }
            ]));
        policyRequirementQuery.GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));
        twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user.Id)))
            .Returns(new List<(Guid userId, bool twoFactorIsEnabled)>() { (user.Id, true) });

        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, "key", confirmingUser.Id);

        await sutProvider.GetDependency<IEventService>().Received(1).LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
        await sutProvider.GetDependency<ISendOrganizationConfirmationCommand>().Received(1).SendConfirmationAsync(org, user.Email, orgUser.AccessSecretsManager);
        await organizationUserRepository.Received(1).ReplaceManyAsync(Arg.Is<List<OrganizationUser>>(users => users.Contains(orgUser) && users.Count == 1));
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithOrganizationDataOwnershipPolicyApplicable_WithValidCollectionName_CreatesDefaultCollection(
        Organization organization, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        string key, string collectionName, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        organization.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = organization.Id;
        orgUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        sutProvider.GetDependency<IUserRepository>().GetManyAsync(default).ReturnsForAnyArgs(new[] { user });

        var policyDetails = new PolicyDetails
        {
            OrganizationId = organization.Id,
            OrganizationUserId = orgUser.Id,
            IsProvider = false,
            OrganizationUserStatus = orgUser.Status,
            OrganizationUserType = orgUser.Type,
            PolicyType = PolicyType.OrganizationDataOwnership
        };
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(
                Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUser.UserId!.Value)))
            .Returns([(orgUser.UserId!.Value, new OrganizationDataOwnershipPolicyRequirement(OrganizationDataOwnershipState.Enabled, [policyDetails]))]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, collectionName);

        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .CreateDefaultCollectionsAsync(
                organization.Id,
                Arg.Is<IEnumerable<Guid>>(ids => ids.Single() == orgUser.Id),
                collectionName);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithOrganizationDataOwnershipPolicyApplicable_WithInvalidCollectionName_DoesNotCreateDefaultCollection(
        Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IUserRepository>().GetManyAsync(default).ReturnsForAnyArgs(new[] { user });

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, "");

        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .CreateDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithOrganizationDataOwnershipPolicyNotApplicable_DoesNotCreateDefaultCollection(
        Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted, OrganizationUserType.Owner)] OrganizationUser orgUser, User user,
        string key, string collectionName, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        sutProvider.GetDependency<IUserRepository>().GetManyAsync(default).ReturnsForAnyArgs(new[] { user });

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(
                Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUser.UserId!.Value)))
            .Returns([(orgUser.UserId!.Value, new OrganizationDataOwnershipPolicyRequirement(OrganizationDataOwnershipState.Disabled, []))]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(orgUser.UserId!.Value)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, collectionName);

        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .CreateDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithAutoConfirmEnabledAndUserBelongsToAnotherOrg_ThrowsBadRequest(
        Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        OrganizationUser otherOrgUser, string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;
        otherOrgUser.UserId = user.Id;
        otherOrgUser.OrganizationId = Guid.NewGuid(); // Different org

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync([]).ReturnsForAnyArgs([orgUser]);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync([])
            .ReturnsForAnyArgs([orgUser, otherOrgUser]);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IUserRepository>().GetManyAsync([]).ReturnsForAnyArgs([user]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([new PolicyDetails { OrganizationId = org.Id }]));

        sutProvider.GetDependency<IAutomaticUserConfirmationPolicyEnforcementValidator>()
            .IsCompliantAsync(Arg.Any<AutomaticUserConfirmationPolicyEnforcementRequest>(), Arg.Any<AutomaticUserConfirmationPolicyRequirement>())
            .Returns(Invalid(
                new AutomaticUserConfirmationPolicyEnforcementRequest(orgUser.Id, [orgUser, otherOrgUser], user),
                new UserCannotBelongToAnotherOrganization()));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id));

        Assert.Equal(new UserCannotBelongToAnotherOrganization().Message, exception.Message);
        await sutProvider.GetDependency<IDeleteEmergencyAccessCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAllByUserIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithAutoConfirmEnabledForOtherOrg_ThrowsBadRequest(
        Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        OrganizationUser otherOrgUser, string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        // Arrange
        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;
        otherOrgUser.UserId = user.Id;
        otherOrgUser.OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync([]).ReturnsForAnyArgs([orgUser]);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync([])
            .ReturnsForAnyArgs([orgUser, otherOrgUser]);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IUserRepository>().GetManyAsync([]).ReturnsForAnyArgs([user]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([new PolicyDetails { OrganizationId = org.Id }]));

        sutProvider.GetDependency<IAutomaticUserConfirmationPolicyEnforcementValidator>()
            .IsCompliantAsync(Arg.Any<AutomaticUserConfirmationPolicyEnforcementRequest>(), Arg.Any<AutomaticUserConfirmationPolicyRequirement>())
            .Returns(Invalid(
                new AutomaticUserConfirmationPolicyEnforcementRequest(org.Id, [orgUser, otherOrgUser], user),
                new OtherOrganizationDoesNotAllowOtherMembership()));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id));

        Assert.Equal(new OtherOrganizationDoesNotAllowOtherMembership().Message, exception.Message);
        await sutProvider.GetDependency<IDeleteEmergencyAccessCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAllByUserIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithAutoConfirmEnabledAndUserIsProvider_ThrowsBadRequest(
        Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        // Arrange
        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync([]).ReturnsForAnyArgs([orgUser]);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync([])
            .ReturnsForAnyArgs([orgUser]);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IUserRepository>().GetManyAsync([]).ReturnsForAnyArgs([user]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([new PolicyDetails { OrganizationId = org.Id }]));

        sutProvider.GetDependency<IAutomaticUserConfirmationPolicyEnforcementValidator>()
            .IsCompliantAsync(Arg.Any<AutomaticUserConfirmationPolicyEnforcementRequest>(), Arg.Any<AutomaticUserConfirmationPolicyRequirement>())
            .Returns(Invalid(
                new AutomaticUserConfirmationPolicyEnforcementRequest(org.Id, [orgUser], user),
                new ProviderUsersCannotJoin()));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id));

        Assert.Equal(new ProviderUsersCannotJoin().Message, exception.Message);
        await sutProvider.GetDependency<IDeleteEmergencyAccessCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAllByUserIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithAutoConfirmNotApplicable_Succeeds(
        Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        // Arrange
        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync([]).ReturnsForAnyArgs([orgUser]);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync([])
            .ReturnsForAnyArgs([orgUser]);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IUserRepository>().GetManyAsync([]).ReturnsForAnyArgs([user]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([new PolicyDetails { OrganizationId = org.Id }]));

        sutProvider.GetDependency<IAutomaticUserConfirmationPolicyEnforcementValidator>()
            .IsCompliantAsync(Arg.Any<AutomaticUserConfirmationPolicyEnforcementRequest>(), Arg.Any<AutomaticUserConfirmationPolicyRequirement>())
            .Returns(Valid(new AutomaticUserConfirmationPolicyEnforcementRequest(org.Id, [orgUser], user)));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        // Act
        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id);

        // Assert
        await sutProvider.GetDependency<IEventService>()
            .Received(1).LogOrganizationUserEventAsync(orgUser, EventType.OrganizationUser_Confirmed);
        await sutProvider.GetDependency<ISendOrganizationConfirmationCommand>()
            .Received(1).SendConfirmationAsync(org, user.Email, orgUser.AccessSecretsManager);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithAutoConfirmPolicyEnabled_DeletesEmergencyAccess(
        Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        // Arrange
        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync([]).ReturnsForAnyArgs([orgUser]);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync([])
            .ReturnsForAnyArgs([orgUser]);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IUserRepository>().GetManyAsync([]).ReturnsForAnyArgs([user]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([new PolicyDetails { OrganizationId = org.Id }]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(SingleOrganizationPolicyRequirementTestFactory.NoSinglePolicyOrganizationsForUser());

        sutProvider.GetDependency<IAutomaticUserConfirmationPolicyEnforcementValidator>()
            .IsCompliantAsync(Arg.Any<AutomaticUserConfirmationPolicyEnforcementRequest>(), Arg.Any<AutomaticUserConfirmationPolicyRequirement>())
            .Returns(Valid(new AutomaticUserConfirmationPolicyEnforcementRequest(org.Id, [orgUser], user)));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        // Act
        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id);

        // Assert
        await sutProvider.GetDependency<IDeleteEmergencyAccessCommand>()
            .Received(1)
            .DeleteAllByUserIdAsync(user.Id);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithAutoConfirmPolicyNotEnabled_DoesNotDeleteEmergencyAccess(
        Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        // Arrange
        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync([]).ReturnsForAnyArgs([orgUser]);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync([])
            .ReturnsForAnyArgs([orgUser]);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IUserRepository>().GetManyAsync([]).ReturnsForAnyArgs([user]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(user.Id)
            .Returns(SingleOrganizationPolicyRequirementTestFactory.NoSinglePolicyOrganizationsForUser());

        sutProvider.GetDependency<IAutomaticUserConfirmationPolicyEnforcementValidator>()
            .IsCompliantAsync(Arg.Any<AutomaticUserConfirmationPolicyEnforcementRequest>(), Arg.Any<AutomaticUserConfirmationPolicyRequirement>())
            .Returns(Valid(new AutomaticUserConfirmationPolicyEnforcementRequest(org.Id, [orgUser], user)));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        // Act
        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id);

        // Assert
        await sutProvider.GetDependency<IDeleteEmergencyAccessCommand>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAllByUserIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_WithAutoConfirmValidationBeforeSingleOrgPolicy_ChecksAutoConfirmFirst(
        Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        OrganizationUser otherOrgUser,
        string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        // Arrange - Setup conditions that would fail BOTH auto-confirm AND single org policy
        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser.UserId = user.Id;
        otherOrgUser.UserId = user.Id;
        otherOrgUser.OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync([]).ReturnsForAnyArgs([orgUser]);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync([])
            .ReturnsForAnyArgs([orgUser, otherOrgUser]);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IUserRepository>().GetManyAsync([]).ReturnsForAnyArgs([user]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(user.Id)
            .Returns(new AutomaticUserConfirmationPolicyRequirement([new PolicyDetails { OrganizationId = org.Id }]));

        sutProvider.GetDependency<IAutomaticUserConfirmationPolicyEnforcementValidator>()
            .IsCompliantAsync(Arg.Any<AutomaticUserConfirmationPolicyEnforcementRequest>(), Arg.Any<AutomaticUserConfirmationPolicyRequirement>())
            .Returns(Invalid(
                new AutomaticUserConfirmationPolicyEnforcementRequest(org.Id, [orgUser, otherOrgUser], user),
                new UserCannotBelongToAnotherOrganization()));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id));

        Assert.Equal(new UserCannotBelongToAnotherOrganization().Message, exception.Message);
        Assert.NotEqual("Cannot confirm this member to the organization until they leave or remove all other organizations.",
            exception.Message);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUsersAsync_WithAutoConfirmEnabled_MixedResults(
        Organization org, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser2,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser3,
        OrganizationUser otherOrgUser, User user1, User user2, User user3,
        string key, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        // Arrange
        org.PlanType = PlanType.EnterpriseAnnually;
        orgUser1.OrganizationId = orgUser2.OrganizationId = orgUser3.OrganizationId = confirmingUser.OrganizationId = org.Id;
        orgUser1.UserId = user1.Id;
        orgUser2.UserId = user2.Id;
        orgUser3.UserId = user3.Id;
        otherOrgUser.UserId = user3.Id;
        otherOrgUser.OrganizationId = Guid.NewGuid();

        var orgUsers = new[] { orgUser1, orgUser2, orgUser3 };
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync([]).ReturnsForAnyArgs(orgUsers);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(org.Id).Returns(org);
        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync([]).ReturnsForAnyArgs([user1, user2, user3]);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByManyUsersAsync([])
            .ReturnsForAnyArgs([orgUser1, orgUser2, orgUser3, otherOrgUser]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<AutomaticUserConfirmationPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new AutomaticUserConfirmationPolicyRequirement([new PolicyDetails { OrganizationId = org.Id }]));

        sutProvider.GetDependency<IAutomaticUserConfirmationPolicyEnforcementValidator>()
            .IsCompliantAsync(Arg.Is<AutomaticUserConfirmationPolicyEnforcementRequest>(r => r.User.Id == user1.Id), Arg.Any<AutomaticUserConfirmationPolicyRequirement>())
            .Returns(Valid(new AutomaticUserConfirmationPolicyEnforcementRequest(org.Id, [orgUser1], user1)));

        sutProvider.GetDependency<IAutomaticUserConfirmationPolicyEnforcementValidator>()
            .IsCompliantAsync(Arg.Is<AutomaticUserConfirmationPolicyEnforcementRequest>(r => r.User.Id == user2.Id), Arg.Any<AutomaticUserConfirmationPolicyRequirement>())
            .Returns(Valid(new AutomaticUserConfirmationPolicyEnforcementRequest(org.Id, [orgUser2], user2)));

        sutProvider.GetDependency<IAutomaticUserConfirmationPolicyEnforcementValidator>()
            .IsCompliantAsync(Arg.Is<AutomaticUserConfirmationPolicyEnforcementRequest>(r => r.User.Id == user3.Id), Arg.Any<AutomaticUserConfirmationPolicyRequirement>())
            .Returns(Invalid(
                new AutomaticUserConfirmationPolicyEnforcementRequest(org.Id, [orgUser3, otherOrgUser], user3),
                new OtherOrganizationDoesNotAllowOtherMembership()));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new SingleOrganizationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        var keys = orgUsers.ToDictionary(ou => ou.Id, _ => key);

        // Act
        var result = await sutProvider.Sut.ConfirmUsersAsync(confirmingUser.OrganizationId, keys, confirmingUser.Id);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Empty(result[0].Item2);
        Assert.Empty(result[1].Item2);
        Assert.Equal(new OtherOrganizationDoesNotAllowOtherMembership().Message, result[2].Item2);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_UseMyItemsDisabled_DoesNotCreateDefaultCollection(
        Organization organization, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        string key, string collectionName, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually;
        organization.UseMyItems = false;
        orgUser.OrganizationId = confirmingUser.OrganizationId = organization.Id;
        orgUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        sutProvider.GetDependency<IUserRepository>().GetManyAsync(default).ReturnsForAnyArgs(new[] { user });

        var policyDetails = new PolicyDetails
        {
            OrganizationId = organization.Id,
            OrganizationUserId = orgUser.Id,
            IsProvider = false,
            OrganizationUserStatus = orgUser.Status,
            OrganizationUserType = orgUser.Type,
            PolicyType = PolicyType.OrganizationDataOwnership
        };
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(orgUser.UserId!.Value)
            .Returns(new OrganizationDataOwnershipPolicyRequirement(OrganizationDataOwnershipState.Enabled, [policyDetails]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(orgUser.UserId!.Value)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        // Act
        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, collectionName);

        // Assert - Collection repository should NOT be called
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .CreateDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ConfirmUserAsync_UseMyItemsEnabled_CreatesDefaultCollection(
        Organization organization, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser, User user,
        string key, string collectionName, SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually;
        organization.UseMyItems = true;
        orgUser.OrganizationId = confirmingUser.OrganizationId = organization.Id;
        orgUser.UserId = user.Id;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser });
        sutProvider.GetDependency<IUserRepository>().GetManyAsync(default).ReturnsForAnyArgs(new[] { user });

        var policyDetails = new PolicyDetails
        {
            OrganizationId = organization.Id,
            OrganizationUserId = orgUser.Id,
            IsProvider = false,
            OrganizationUserStatus = orgUser.Status,
            OrganizationUserType = orgUser.Type,
            PolicyType = PolicyType.OrganizationDataOwnership
        };
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUser.UserId!.Value)))
            .Returns([
                (orgUser.UserId!.Value, new OrganizationDataOwnershipPolicyRequirement(OrganizationDataOwnershipState.Enabled, [policyDetails]))
            ]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(orgUser.UserId!.Value)
            .Returns(new SingleOrganizationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        // Act
        await sutProvider.Sut.ConfirmUserAsync(orgUser.OrganizationId, orgUser.Id, key, confirmingUser.Id, collectionName);

        // Assert - Collection repository should be called
        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .CreateDefaultCollectionsAsync(
                organization.Id,
                Arg.Is<IEnumerable<Guid>>(ids => ids.Single() == orgUser.Id),
                collectionName);
    }

    [Theory, BitAutoData]
    public async Task ConfirmUsersAsync_UseMyItemsDisabled_DoesNotCreateDefaultCollections(
        Organization organization, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser2,
        User user1, User user2, string key1, string key2, string collectionName,
        SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually;
        organization.UseMyItems = false;
        orgUser1.OrganizationId = confirmingUser.OrganizationId = organization.Id;
        orgUser2.OrganizationId = organization.Id;
        orgUser1.UserId = user1.Id;
        orgUser2.UserId = user2.Id;

        var keys = new Dictionary<Guid, string>
        {
            { orgUser1.Id, key1 },
            { orgUser2.Id, key2 }
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser1, orgUser2 });
        sutProvider.GetDependency<IUserRepository>().GetManyAsync(default).ReturnsForAnyArgs(new[] { user1, user2 });

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new SingleOrganizationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        // Act
        await sutProvider.Sut.ConfirmUsersAsync(organization.Id, keys, confirmingUser.Id, collectionName);

        // Assert - Collection repository should NOT be called
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .CreateDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ConfirmUsersAsync_UseMyItemsEnabled_CreatesDefaultCollections(
        Organization organization, OrganizationUser confirmingUser,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser2,
        User user1, User user2, string key1, string key2, string collectionName,
        SutProvider<ConfirmOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually;
        organization.UseMyItems = true;
        orgUser1.OrganizationId = confirmingUser.OrganizationId = organization.Id;
        orgUser2.OrganizationId = organization.Id;
        orgUser1.UserId = user1.Id;
        orgUser2.UserId = user2.Id;

        var keys = new Dictionary<Guid, string>
        {
            { orgUser1.Id, key1 },
            { orgUser2.Id, key2 }
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(default).ReturnsForAnyArgs(new[] { orgUser1, orgUser2 });
        sutProvider.GetDependency<IUserRepository>().GetManyAsync(default).ReturnsForAnyArgs(new[] { user1, user2 });

        var policyDetails1 = new PolicyDetails
        {
            OrganizationId = organization.Id,
            OrganizationUserId = orgUser1.Id,
            IsProvider = false,
            OrganizationUserStatus = orgUser1.Status,
            OrganizationUserType = orgUser1.Type,
            PolicyType = PolicyType.OrganizationDataOwnership
        };
        var policyDetails2 = new PolicyDetails
        {
            OrganizationId = organization.Id,
            OrganizationUserId = orgUser2.Id,
            IsProvider = false,
            OrganizationUserStatus = orgUser2.Status,
            OrganizationUserType = orgUser2.Type,
            PolicyType = PolicyType.OrganizationDataOwnership
        };
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUser1.UserId!.Value) && ids.Contains(orgUser2.UserId!.Value)))
            .Returns([
                (orgUser1.UserId!.Value, new OrganizationDataOwnershipPolicyRequirement(OrganizationDataOwnershipState.Enabled, [policyDetails1])),
                (orgUser2.UserId!.Value, new OrganizationDataOwnershipPolicyRequirement(OrganizationDataOwnershipState.Enabled, [policyDetails2]))
            ]);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<SingleOrganizationPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new SingleOrganizationPolicyRequirement([]));

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<RequireTwoFactorPolicyRequirement>(Arg.Any<Guid>())
            .Returns(new RequireTwoFactorPolicyRequirement([]));

        // Act
        await sutProvider.Sut.ConfirmUsersAsync(organization.Id, keys, confirmingUser.Id, collectionName);

        // Assert - Collection repository should be called with correct parameters
        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .CreateDefaultCollectionsAsync(
                organization.Id,
                Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == 2 && ids.Contains(orgUser1.Id) && ids.Contains(orgUser2.Id)),
                collectionName);
    }
}
