namespace Bit.Core.Settings;

public interface IPasswordlessAuthSettings
{
    bool KnownDevicesOnly { get; set; }
}
