using Bit.Core.Enums;

namespace Bit.Seeder.Factories;

internal static class DeviceSeeder
{
    internal static Core.Entities.Device Create(Guid userId, DeviceType deviceType, string deviceName, string identifier, string? pushToken)
    {
        return new Core.Entities.Device
        {
            Id = Core.Utilities.CoreHelpers.GenerateComb(),
            UserId = userId,
            Type = deviceType,
            Name = deviceName,
            Identifier = identifier,
            PushToken = pushToken
        };
    }
}
