namespace Bit.Icons;

/// <summary>
/// Service keys for the memory caches used by the Icons service. Each cache is registered as a
/// separate keyed <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/> so that the icon
/// and change-password-URI caches are sized and evicted independently of one another.
/// </summary>
public static class IconsCacheConstants
{
    /// <summary>
    /// The cache used by the icons endpoint to store fetched favicons.
    /// </summary>
    public const string IconsCacheName = "Icons";

    /// <summary>
    /// The cache used by the change-password-URI endpoint to store resolved URIs.
    /// </summary>
    public const string ChangePasswordUriCacheName = "ChangePasswordUri";
}
