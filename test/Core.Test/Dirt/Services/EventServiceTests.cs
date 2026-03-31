using Bit.Core.AdminConsole.Context;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class EventServiceTests
{
    public static IEnumerable<object[]> InstallationIdTestCases => TestCaseHelper.GetCombinationsOfMultipleLists(
        new object[] { Guid.NewGuid(), null },
        Enum.GetValues<EventType>().Select(e => (object)e)
    ).Select(p => p.ToArray());

    [Theory, BitAutoData]
    public async Task LogGroupEvent_LogsRequiredInfo(Group group, EventType eventType, DateTime date,
        Guid actingUserId, Guid providerId, string ipAddress, DeviceType deviceType, SutProvider<EventService> sutProvider)
    {
        var orgAbilities = new Dictionary<Guid, OrganizationAbility>()
        {
            { group.OrganizationId, new OrganizationAbility() { UseEvents = true, Enabled = true } }
        };
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync(Arg.Any<IEnumerable<Guid>>()).Returns(orgAbilities);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().IpAddress.Returns(ipAddress);
        sutProvider.GetDependency<ICurrentContext>().DeviceType.Returns(deviceType);
        sutProvider.GetDependency<ICurrentContext>().ProviderIdForOrg(Arg.Any<Guid>()).Returns(providerId);

        await sutProvider.Sut.LogGroupEventAsync(group, eventType, date);

        var expected = new List<IEvent>() {
            new EventMessage()
            {
                IpAddress = ipAddress,
                DeviceType = deviceType,
                OrganizationId = group.OrganizationId,
                GroupId = group.Id,
                Type = eventType,
                ActingUserId = actingUserId,
                ProviderId = providerId,
                Date = date,
                SystemUser = null
            }
        };

        await sutProvider.GetDependency<IEventWriteService>().Received(1).CreateManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual<IEvent>(expected, new[] { "IdempotencyId" })));
    }

    [Theory, BitAutoData]
    public async Task LogGroupEvent_WithEventSystemUser_LogsRequiredInfo(Group group, EventType eventType, EventSystemUser eventSystemUser, DateTime date,
        Guid actingUserId, Guid providerId, string ipAddress, DeviceType deviceType, SutProvider<EventService> sutProvider)
    {
        var orgAbilities = new Dictionary<Guid, OrganizationAbility>()
        {
            { group.OrganizationId, new OrganizationAbility() { UseEvents = true, Enabled = true } }
        };
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync(Arg.Any<IEnumerable<Guid>>()).Returns(orgAbilities);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().IpAddress.Returns(ipAddress);
        sutProvider.GetDependency<ICurrentContext>().DeviceType.Returns(deviceType);
        sutProvider.GetDependency<ICurrentContext>().ProviderIdForOrg(Arg.Any<Guid>()).Returns(providerId);

        await sutProvider.Sut.LogGroupEventAsync(group, eventType, eventSystemUser, date);

        var eventMessage = new EventMessage()
        {
            IpAddress = ipAddress,
            DeviceType = deviceType,
            OrganizationId = group.OrganizationId,
            GroupId = group.Id,
            Type = eventType,
            ActingUserId = actingUserId,
            ProviderId = providerId,
            Date = date,
            SystemUser = eventSystemUser
        };

        if (eventSystemUser is EventSystemUser.SCIM)
        {
            eventMessage.DeviceType = DeviceType.Server;
        }

        var expected = new List<IEvent>() {
            eventMessage
        };

        await sutProvider.GetDependency<IEventWriteService>().Received(1).CreateManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual<IEvent>(expected, new[] { "IdempotencyId" })));
    }

    [Theory]
    [BitMemberAutoData(nameof(InstallationIdTestCases))]
    public async Task LogOrganizationEvent_ProvidesInstallationId(Guid? installationId, EventType eventType,
        Organization organization, SutProvider<EventService> sutProvider)
    {
        organization.Enabled = true;
        organization.UseEvents = true;

        sutProvider.GetDependency<ICurrentContext>().InstallationId.Returns(installationId);

        await sutProvider.Sut.LogOrganizationEventAsync(organization, eventType);

        await sutProvider.GetDependency<IEventWriteService>().Received(1).CreateAsync(Arg.Is<IEvent>(e =>
            e.OrganizationId == organization.Id &&
            e.Type == eventType &&
            e.InstallationId == installationId));
    }

    [Theory, BitAutoData]
    public async Task LogOrganizationEvent_WithEventSystemUser_LogsRequiredInfo(Organization organization, EventType eventType,
        EventSystemUser eventSystemUser, DateTime date, Guid providerId, SutProvider<EventService> sutProvider)
    {
        organization.Enabled = true;
        organization.UseEvents = true;

        sutProvider.GetDependency<ICurrentContext>().ProviderIdForOrg(Arg.Any<Guid>()).Returns(providerId);

        await sutProvider.Sut.LogOrganizationEventAsync(organization, eventType, eventSystemUser, date);

        await sutProvider.GetDependency<IEventWriteService>().Received(1).CreateAsync(Arg.Is<IEvent>(e =>
            e.OrganizationId == organization.Id &&
            e.Type == eventType &&
            e.SystemUser == eventSystemUser &&
            e.DeviceType == DeviceType.Server &&
            e.Date == date &&
            e.ProviderId == providerId));
    }

    [Theory, BitAutoData]
    public async Task LogOrganizationUserEvent_LogsRequiredInfo(OrganizationUser orgUser, EventType eventType, DateTime date,
        Guid actingUserId, Guid providerId, string ipAddress, DeviceType deviceType, SutProvider<EventService> sutProvider)
    {
        var orgAbilities = new Dictionary<Guid, OrganizationAbility>()
        {
            {orgUser.OrganizationId, new OrganizationAbility() { UseEvents = true, Enabled = true } }
        };
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync(Arg.Any<IEnumerable<Guid>>()).Returns(orgAbilities);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().IpAddress.Returns(ipAddress);
        sutProvider.GetDependency<ICurrentContext>().DeviceType.Returns(deviceType);
        sutProvider.GetDependency<ICurrentContext>().ProviderIdForOrg(Arg.Any<Guid>()).Returns(providerId);

        await sutProvider.Sut.LogOrganizationUserEventAsync(orgUser, eventType, date);

        var expected = new List<IEvent>() {
            new EventMessage()
            {
                IpAddress = ipAddress,
                DeviceType = deviceType,
                OrganizationId = orgUser.OrganizationId,
                UserId = orgUser.UserId,
                OrganizationUserId = orgUser.Id,
                ProviderId = providerId,
                Type = eventType,
                ActingUserId = actingUserId,
                Date = date
            }
        };

        await sutProvider.GetDependency<IEventWriteService>().Received(1).CreateManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual<IEvent>(expected, new[] { "IdempotencyId" })));
    }

    [Theory, BitAutoData]
    public async Task LogOrganizationUserEvent_WithEventSystemUser_LogsRequiredInfo(OrganizationUser orgUser, EventType eventType, EventSystemUser eventSystemUser, DateTime date,
        Guid actingUserId, Guid providerId, string ipAddress, SutProvider<EventService> sutProvider)
    {
        var orgAbilities = new Dictionary<Guid, OrganizationAbility>()
        {
            {orgUser.OrganizationId, new OrganizationAbility() { UseEvents = true, Enabled = true } }
        };
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync(Arg.Any<IEnumerable<Guid>>()).Returns(orgAbilities);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().IpAddress.Returns(ipAddress);
        sutProvider.GetDependency<ICurrentContext>().ProviderIdForOrg(Arg.Any<Guid>()).Returns(providerId);

        await sutProvider.Sut.LogOrganizationUserEventAsync(orgUser, eventType, eventSystemUser, date);

        var expected = new List<IEvent>() {
            new EventMessage()
            {
                IpAddress = ipAddress,
                OrganizationId = orgUser.OrganizationId,
                UserId = orgUser.UserId,
                OrganizationUserId = orgUser.Id,
                ProviderId = providerId,
                Type = eventType,
                ActingUserId = actingUserId,
                Date = date,
                SystemUser = eventSystemUser
            }
        };

        await sutProvider.GetDependency<IEventWriteService>().Received(1).CreateManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual<IEvent>(expected, new[] { "IdempotencyId" })));
    }

    [Theory, BitAutoData]
    public async Task LogProviderUserEvent_LogsRequiredInfo(ProviderUser providerUser, EventType eventType, DateTime date,
        Guid actingUserId, Guid providerId, string ipAddress, DeviceType deviceType, SutProvider<EventService> sutProvider)
    {
        var providerAbilities = new Dictionary<Guid, ProviderAbility>()
        {
            {providerUser.ProviderId, new ProviderAbility() { UseEvents = true, Enabled = true } }
        };
        sutProvider.GetDependency<IApplicationCacheService>().GetProviderAbilitiesAsync().Returns(providerAbilities);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().IpAddress.Returns(ipAddress);
        sutProvider.GetDependency<ICurrentContext>().DeviceType.Returns(deviceType);
        sutProvider.GetDependency<ICurrentContext>().ProviderIdForOrg(Arg.Any<Guid>()).Returns(providerId);

        await sutProvider.Sut.LogProviderUserEventAsync(providerUser, eventType, date);

        var expected = new List<IEvent>() {
            new EventMessage()
            {
                IpAddress = ipAddress,
                DeviceType = deviceType,
                ProviderId = providerUser.ProviderId,
                UserId = providerUser.UserId,
                ProviderUserId = providerUser.Id,
                Type = eventType,
                ActingUserId = actingUserId,
                Date = date
            }
        };

        await sutProvider.GetDependency<IEventWriteService>().Received(1).CreateManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual<IEvent>(expected, new[] { "IdempotencyId" })));
    }

    [Theory, BitAutoData]
    public async Task LogCollectionEvent_LogsRequiredInfo(Collection collection, EventType eventType, DateTime date,
        Guid actingUserId, Guid providerId, string ipAddress, DeviceType deviceType, SutProvider<EventService> sutProvider)
    {
        // Arrange
        var orgAbilities = new Dictionary<Guid, OrganizationAbility>()
        {
            { collection.OrganizationId, new OrganizationAbility() { UseEvents = true, Enabled = true } }
        };
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync(Arg.Any<IEnumerable<Guid>>()).Returns(orgAbilities);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().IpAddress.Returns(ipAddress);
        sutProvider.GetDependency<ICurrentContext>().DeviceType.Returns(deviceType);
        sutProvider.GetDependency<ICurrentContext>().ProviderIdForOrg(Arg.Any<Guid>()).Returns(providerId);

        // Act
        await sutProvider.Sut.LogCollectionEventAsync(collection, eventType, date);

        // Assert
        var expected = new List<IEvent>()
        {
            new EventMessage()
            {
                IpAddress = ipAddress,
                DeviceType = deviceType,
                OrganizationId = collection.OrganizationId,
                CollectionId = collection.Id,
                Type = eventType,
                ActingUserId = actingUserId,
                ProviderId = providerId,
                Date = date
            }
        };

        await sutProvider.GetDependency<IEventWriteService>().Received(1).CreateManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual<IEvent>(expected, new[] { "IdempotencyId" })));
    }

    [Theory, BitAutoData]
    public async Task LogCollectionEvent_WhenEventsDisabled_DoesNotLog(Collection collection, EventType eventType,
        DateTime date, SutProvider<EventService> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, OrganizationAbility>());

        // Act
        await sutProvider.Sut.LogCollectionEventAsync(collection, eventType, date);

        // Assert
        await sutProvider.GetDependency<IEventWriteService>().Received(1).CreateManyAsync(Arg.Is<IEnumerable<IEvent>>(e => !e.Any()));
    }

    [Theory, BitAutoData]
    public async Task LogPolicyEvent_LogsRequiredInfo(Policy policy, EventType eventType, DateTime date,
        Guid actingUserId, Guid providerId, string ipAddress, DeviceType deviceType, SutProvider<EventService> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(policy.OrganizationId)
            .Returns(new OrganizationAbility { UseEvents = true, Enabled = true });
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().IpAddress.Returns(ipAddress);
        sutProvider.GetDependency<ICurrentContext>().DeviceType.Returns(deviceType);
        sutProvider.GetDependency<ICurrentContext>().ProviderIdForOrg(Arg.Any<Guid>()).Returns(providerId);

        // Act
        await sutProvider.Sut.LogPolicyEventAsync(policy, eventType, date);

        // Assert
        await sutProvider.GetDependency<IEventWriteService>().Received(1).CreateAsync(Arg.Is<IEvent>(e =>
            e.OrganizationId == policy.OrganizationId &&
            e.PolicyId == policy.Id &&
            e.Type == eventType &&
            e.ActingUserId == actingUserId &&
            e.ProviderId == providerId &&
            e.Date == date));
    }

    [Theory, BitAutoData]
    public async Task LogPolicyEvent_WhenEventsDisabled_DoesNotLog(Policy policy, EventType eventType,
        DateTime date, SutProvider<EventService> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(policy.OrganizationId)
            .Returns(new OrganizationAbility { UseEvents = false, Enabled = true });

        // Act
        await sutProvider.Sut.LogPolicyEventAsync(policy, eventType, date);

        // Assert
        await sutProvider.GetDependency<IEventWriteService>().DidNotReceiveWithAnyArgs().CreateAsync(default);
    }

    [Theory, BitAutoData]
    public async Task LogProviderOrganizationEventsAsync_LogsRequiredInfo(Provider provider, ICollection<ProviderOrganization> providerOrganizations, EventType eventType, DateTime date,
        Guid actingUserId, Guid providerId, string ipAddress, DeviceType deviceType, SutProvider<EventService> sutProvider)
    {
        foreach (var providerOrganization in providerOrganizations)
        {
            providerOrganization.ProviderId = provider.Id;
        }

        var providerAbilities = new Dictionary<Guid, ProviderAbility>()
        {
            { provider.Id, new ProviderAbility() { UseEvents = true, Enabled = true } }
        };
        sutProvider.GetDependency<IApplicationCacheService>().GetProviderAbilitiesAsync().Returns(providerAbilities);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(actingUserId);
        sutProvider.GetDependency<ICurrentContext>().IpAddress.Returns(ipAddress);
        sutProvider.GetDependency<ICurrentContext>().DeviceType.Returns(deviceType);
        sutProvider.GetDependency<ICurrentContext>().ProviderIdForOrg(Arg.Any<Guid>()).Returns(providerId);

        await sutProvider.Sut.LogProviderOrganizationEventsAsync(providerOrganizations.Select(po => (po, eventType, (DateTime?)date)));

        var expected = providerOrganizations.Select(po =>
            new EventMessage()
            {
                DeviceType = deviceType,
                IpAddress = ipAddress,
                ProviderId = provider.Id,
                ProviderOrganizationId = po.Id,
                Type = eventType,
                ActingUserId = actingUserId,
                Date = date
            }).ToList();

        await sutProvider.GetDependency<IEventWriteService>().Received(1).CreateManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual<IEvent>(expected, new[] { "IdempotencyId" })));
    }

    [Theory, BitAutoData]
    public async Task LogUserEvent_IncludeAcceptedStatusOrgs_AcceptedOrgUser_CreatesOrgScopedEvent(
        Guid userId, EventType eventType, OrganizationUser orgUser, SutProvider<EventService> sutProvider)
    {
        orgUser.UserId = userId;
        orgUser.Status = OrganizationUserStatusType.Accepted;

        var orgAbilities = new Dictionary<Guid, OrganizationAbility>
        {
            { orgUser.OrganizationId, new OrganizationAbility { UseEvents = true, Enabled = true } }
        };

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(orgAbilities);
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetProviderAbilitiesAsync()
            .Returns(new Dictionary<Guid, ProviderAbility>());
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser> { orgUser });
        sutProvider.GetDependency<ICurrentContext>()
            .ProviderMembershipAsync(Arg.Any<IProviderUserRepository>(), userId)
            .Returns(new List<CurrentContextProvider>());

        await sutProvider.Sut.LogUserEventAsync(userId, eventType, includeAcceptedStatusOrgs: true);

        await sutProvider.GetDependency<IEventWriteService>()
            .Received(1)
            .CreateManyAsync(Arg.Is<IEnumerable<IEvent>>(events =>
                events.Count() == 2
                && events.Any(e => e.OrganizationId == null && e.UserId == userId && e.Type == eventType)
                && events.Any(e => e.OrganizationId == orgUser.OrganizationId && e.UserId == userId && e.Type == eventType)));
    }

    [Theory, BitAutoData]
    public async Task LogUserEvent_IncludeAcceptedStatusOrgs_InvitedOrgUser_DoesNotCreateOrgScopedEvent(
        Guid userId, EventType eventType, OrganizationUser orgUser, SutProvider<EventService> sutProvider)
    {
        orgUser.UserId = userId;
        orgUser.Status = OrganizationUserStatusType.Invited;

        var orgAbilities = new Dictionary<Guid, OrganizationAbility>
        {
            { orgUser.OrganizationId, new OrganizationAbility { UseEvents = true, Enabled = true } }
        };

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilitiesAsync()
            .Returns(orgAbilities);
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetProviderAbilitiesAsync()
            .Returns(new Dictionary<Guid, ProviderAbility>());
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(userId)
            .Returns(new List<OrganizationUser> { orgUser });
        sutProvider.GetDependency<ICurrentContext>()
            .ProviderMembershipAsync(
            Arg.Any<IProviderUserRepository>(), userId)
            .Returns(new List<CurrentContextProvider>());

        await sutProvider.Sut.LogUserEventAsync(userId, eventType, includeAcceptedStatusOrgs: true);

        await sutProvider.GetDependency<IEventWriteService>()
            .Received(1)
            .CreateAsync(Arg.Is<IEvent>(e =>
                e.OrganizationId == null && e.UserId == userId && e.Type == eventType));
        await sutProvider.GetDependency<IEventWriteService>()
            .DidNotReceiveWithAnyArgs()
            .CreateManyAsync(default);
    }
}
