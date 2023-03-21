namespace Bit.Core.Auth.Settings;

public interface IPasswordlessAuthSettings
{
    bool KnownDevicesOnly { get; set; }
}
