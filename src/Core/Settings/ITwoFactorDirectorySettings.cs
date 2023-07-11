namespace Bit.Core.Settings;

public interface ITwoFactorDirectorySettings
{
    public string Uri { get; set; }
    public int CacheExpirationHours { get; set; }
}
