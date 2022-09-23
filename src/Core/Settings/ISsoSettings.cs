namespace Bit.Core.Settings;

public interface ISsoSettings
{
    int CacheLifetimeInSeconds { get; set; }
    double SsoTokenLifetimeInSeconds { get; set; }
}
