using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    [SutProviderCustomize]
    public class EventServiceTests
    {
        public static IEnumerable<object[]> InstallationIdTestCases => TestCaseHelper.GetCombinationsOfMultipleLists(
            new object[] { Guid.NewGuid(), null },
            Enum.GetValues<EventType>().Select(e => (object)e)
        ).Select(p => p.ToArray());

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
    }
}
