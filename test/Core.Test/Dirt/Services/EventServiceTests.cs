using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
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
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync().Returns(orgAbilities);
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
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync().Returns(orgAbilities);
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
    public async Task LogOrganizationUserEvent_LogsRequiredInfo(OrganizationUser orgUser, EventType eventType, DateTime date,
        Guid actingUserId, Guid providerId, string ipAddress, DeviceType deviceType, SutProvider<EventService> sutProvider)
    {
        var orgAbilities = new Dictionary<Guid, OrganizationAbility>()
        {
            {orgUser.OrganizationId, new OrganizationAbility() { UseEvents = true, Enabled = true } }
        };
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync().Returns(orgAbilities);
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
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync().Returns(orgAbilities);
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
}
