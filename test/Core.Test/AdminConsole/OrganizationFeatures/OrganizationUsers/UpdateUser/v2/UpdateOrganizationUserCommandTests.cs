using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using Organization = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.UpdateUser.v2;

[SutProviderCustomize]
public class UpdateOrganizationUserCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateUserAsync_WhenValidatorReturnsError_ReturnsErrorAndDoesNotPersist(
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        var request = Setup(sutProvider, organization, organizationUser, valid: false);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<MustHaveConfirmedOwner>(result.AsError);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default, default(IEnumerable<CollectionAccessSelection>));
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpdateGroupsAsync(default, default, default);
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(default(OrganizationUser), default);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateUserAsync_OnSuccessWithGroups_ReplacesUserUpdatesGroupsAndLogsEvent(
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser,
        Guid groupId)
    {
        var request = Setup(sutProvider, organization, organizationUser, groups: [groupId]);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceAsync(organizationUser, Arg.Any<IEnumerable<CollectionAccessSelection>>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .UpdateGroupsAsync(organizationUser.Id, Arg.Any<IEnumerable<Guid>>(), Arg.Any<DateTime>());
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_Updated);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateUserAsync_OnSuccessWithNullGroups_DoesNotUpdateGroups(
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        var request = Setup(sutProvider, organization, organizationUser, groups: null);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .ReplaceAsync(organizationUser, Arg.Any<IEnumerable<CollectionAccessSelection>>());
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpdateGroupsAsync(default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateUserAsync_AppliesRequestedChangesToTheDatabaseCopy(
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        organizationUser.AccessSecretsManager = false;
        var request = Setup(sutProvider, organization, organizationUser,
            type: OrganizationUserType.Admin, targetAccessSecretsManager: true);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrganizationUserType.Admin, organizationUser.Type);
        Assert.True(organizationUser.AccessSecretsManager);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateUserAsync_WhenEnablingSecretsManager_ChecksRequiredSeats(
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        organizationUser.AccessSecretsManager = false;
        var request = Setup(sutProvider, organization, organizationUser, targetAccessSecretsManager: true);

        sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>()
            .CountNewSmSeatsRequiredAsync(organization.Id, 1)
            .Returns(0);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>()
            .Received(1)
            .CountNewSmSeatsRequiredAsync(organization.Id, 1);
        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateUserAsync_WhenSecretsManagerAccessUnchanged_DoesNotCheckSeats(
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        organizationUser.AccessSecretsManager = false;
        var request = Setup(sutProvider, organization, organizationUser, targetAccessSecretsManager: false);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);

        await sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>()
            .DidNotReceiveWithAnyArgs()
            .CountNewSmSeatsRequiredAsync(default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateUserAsync_WhenEnablingSecretsManagerRequiresSeatsOnSelfHost_ReturnsErrorAndDoesNotUpdateSubscriptionOrPersist(
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser)
    {
        organizationUser.AccessSecretsManager = false;
        var request = Setup(sutProvider, organization, organizationUser, targetAccessSecretsManager: true);

        sutProvider.GetDependency<IGlobalSettings>().SelfHosted.Returns(true);
        sutProvider.GetDependency<ICountNewSmSeatsRequiredQuery>()
            .CountNewSmSeatsRequiredAsync(organization.Id, 1)
            .Returns(1);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CannotAutoscaleSecretsManagerSeatsOnSelfHost>(result.AsError);

        // A self-hosted instance must never attempt a subscription update, and nothing should be persisted.
        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionAsync(default);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default, default(IEnumerable<CollectionAccessSelection>));
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventAsync(default(OrganizationUser), default);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task UpdateUserAsync_WhenDemotingPrivilegedUser_WithNameAndUseMyItemsAndPolicyEnabled_CreatesDefaultCollection(
        OrganizationUserType existingType,
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed)] OrganizationUser organizationUser,
        string defaultUserCollectionName)
    {
        organization.UseMyItems = true;
        organizationUser.Type = existingType;
        var request = Setup(sutProvider, organization, organizationUser,
            type: OrganizationUserType.User, defaultUserCollectionName: defaultUserCollectionName);
        SetupDataOwnershipPolicy(sutProvider, organizationUser.UserId!.Value, OrganizationDataOwnershipState.Enabled);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1).CreateDefaultCollectionsAsync(
            organizationUser.OrganizationId,
            Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(organizationUser.Id)),
            defaultUserCollectionName);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task UpdateUserAsync_WhenDemotingPrivilegedUser_WithUseMyItemsDisabled_DoesNotCreateDefaultCollection(
        OrganizationUserType existingType,
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed)] OrganizationUser organizationUser,
        string defaultUserCollectionName)
    {
        organization.UseMyItems = false;
        organizationUser.Type = existingType;
        var request = Setup(sutProvider, organization, organizationUser,
            type: OrganizationUserType.User, defaultUserCollectionName: defaultUserCollectionName);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .CreateDefaultCollectionsAsync(default, default, default);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task UpdateUserAsync_WhenDemotingPrivilegedUser_WithPolicyDisabled_DoesNotCreateDefaultCollection(
        OrganizationUserType existingType,
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed)] OrganizationUser organizationUser,
        string defaultUserCollectionName)
    {
        organization.UseMyItems = true;
        organizationUser.Type = existingType;
        var request = Setup(sutProvider, organization, organizationUser,
            type: OrganizationUserType.User, defaultUserCollectionName: defaultUserCollectionName);
        SetupDataOwnershipPolicy(sutProvider, organizationUser.UserId!.Value, OrganizationDataOwnershipState.Disabled);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .CreateDefaultCollectionsAsync(default, default, default);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task UpdateUserAsync_WhenExistingUserIsNotPrivileged_DoesNotCreateDefaultCollection(
        OrganizationUserType existingType,
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed)] OrganizationUser organizationUser,
        string defaultUserCollectionName)
    {
        organization.UseMyItems = true;
        organizationUser.Type = existingType;
        var request = Setup(sutProvider, organization, organizationUser,
            type: OrganizationUserType.User, defaultUserCollectionName: defaultUserCollectionName);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .CreateDefaultCollectionsAsync(default, default, default);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task UpdateUserAsync_WhenDemotingPrivilegedUser_WithoutName_DoesNotCreateDefaultCollection(
        OrganizationUserType existingType,
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed)] OrganizationUser organizationUser)
    {
        organization.UseMyItems = true;
        organizationUser.Type = existingType;
        var request = Setup(sutProvider, organization, organizationUser, type: OrganizationUserType.User);

        var result = await sutProvider.Sut.UpdateUserAsync(request);

        Assert.True(result.IsSuccess);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .CreateDefaultCollectionsAsync(default, default, default);
    }

    private static UpdateOrganizationUserRequest Setup(
        SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization,
        OrganizationUser organizationUser,
        OrganizationUserType type = OrganizationUserType.User,
        List<CollectionAccessSelection> collections = null,
        IEnumerable<Guid> groups = null,
        bool valid = true,
        bool targetAccessSecretsManager = false,
        string defaultUserCollectionName = null)
    {
        organization.PlanType = PlanType.EnterpriseAnnually;
        organizationUser.OrganizationId = organization.Id;

        sutProvider.GetDependency<IUpdateOrganizationUserValidator>()
            .ValidateAsync(Arg.Any<UpdateOrganizationUserRequest>())
            .Returns(ci => valid
                ? ValidationResultHelpers.Valid(ci.Arg<UpdateOrganizationUserRequest>())
                : ValidationResultHelpers.Invalid(ci.Arg<UpdateOrganizationUserRequest>(), new MustHaveConfirmedOwner()));

        return new UpdateOrganizationUserRequest(
            organizationUser,
            organization,
            new OrganizationAbility { Id = organization.Id },
            [],
            [],
            type,
            null,
            targetAccessSecretsManager,
            collections,
            groups,
            new StandardUser(organizationUser.UserId ?? Guid.NewGuid(), true),
            new OrganizationUser { Type = OrganizationUserType.Owner },
            defaultUserCollectionName);
    }

    private static void SetupDataOwnershipPolicy(SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Guid userId, OrganizationDataOwnershipState state)
    {
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(userId)
            .Returns(new OrganizationDataOwnershipPolicyRequirement(state, []));
    }
}
