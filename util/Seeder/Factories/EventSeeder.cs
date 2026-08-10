using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Seeder.Models;

namespace Bit.Seeder.Factories;

internal static class EventSeeder
{
    internal static Event Create(EventSeed seed)
    {
        return new Event
        {
            Id = CombGuid.Generate(),
            OrganizationId = seed.OrganizationId,
            Type = seed.Type,
            Date = seed.Date,
            ActingUserId = seed.ActingUserId
        };
    }
}
