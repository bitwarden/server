namespace Bit.Core.Settings;
public class SsoSettings : ISsoSettings
{
    public int CacheLifetimeInSeconds { get; set; } = 60;
    public double SsoTokenLifetimeInSeconds { get; set; } = 5;
    public bool EnforceSsoPolicyForAllUsers { get; set; }
}

