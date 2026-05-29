using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Seeder.Factories;

internal static class DeviceSeeder
{
    internal static Device Create(Guid userId, DeviceType deviceType, string deviceName, string identifier, string? pushToken)
    {
        return new Device
        {
            Id = CoreHelpers.GenerateComb(),
            UserId = userId,
            Type = deviceType,
            Name = deviceName,
            Identifier = identifier,
            PushToken = pushToken,
            // Mirror production creation sites — device creation counts as first activity.
            LastActivityDate = DateTime.UtcNow,
        };
    }
}
