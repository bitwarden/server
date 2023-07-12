namespace Bit.Core.Settings;

public interface ITwoFactorDirectorySettings
{
    public Uri Uri { get; set; }
    public int CacheExpirationHours { get; set; }
}
