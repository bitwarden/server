using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Seeder.Factories;

internal static class EventSeeder
{
    internal static Event Create(
        Guid organizationId,
        EventType type,
        DateTime date,
        Guid? actingUserId = null)
    {
        return new Event
        {
            Id = CombGuid.Generate(),
            OrganizationId = organizationId,
            Type = type,
            Date = date,
            ActingUserId = actingUserId
        };
    }
}
