using Bit.Core.Auth.Settings;

namespace Bit.Core.Settings;

public class PasswordlessAuthSettings : IPasswordlessAuthSettings
{
    public bool KnownDevicesOnly { get; set; } = true;
    public TimeSpan UserRequestExpiration { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan AdminRequestExpiration { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan AfterAdminApprovalExpiration { get; set; } = TimeSpan.FromHours(12);
}

