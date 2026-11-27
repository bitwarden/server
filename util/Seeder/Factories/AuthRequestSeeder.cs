using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Seeder.Factories;

internal static class AuthRequestSeeder
{
    private const int _accessCodeLength = 25;

    internal static AuthRequest CreatePending(
        Guid userId,
        Guid? organizationId,
        AuthRequestType type,
        DeviceType requestDeviceType,
        string requestDeviceIdentifier,
        string requestIpAddress,
        string? requestCountryName,
        string publicKey)
    {
        return new AuthRequest
        {
            Id = CombGuid.Generate(),
            UserId = userId,
            OrganizationId = organizationId,
            Type = type,
            RequestDeviceType = requestDeviceType,
            RequestDeviceIdentifier = requestDeviceIdentifier,
            RequestIpAddress = requestIpAddress,
            RequestCountryName = requestCountryName,
            AccessCode = CoreHelpers.SecureRandomString(_accessCodeLength),
            PublicKey = publicKey,
            CreationDate = DateTime.UtcNow,
        };
    }
}
