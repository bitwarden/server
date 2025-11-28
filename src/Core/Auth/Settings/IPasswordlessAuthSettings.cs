namespace Bit.Core.Auth.Settings;

public interface IPasswordlessAuthSettings
{
    bool KnownDevicesOnly { get; set; }
    TimeSpan UserRequestExpiration { get; set; }
    TimeSpan AdminRequestExpiration { get; set; }
    TimeSpan AfterAdminApprovalExpiration { get; set; }
}
