namespace Bit.Core.Settings;

public interface ITwoFactorAuthSettings
{
    bool EmailOnNewDeviceLogin { get; set; }
}
