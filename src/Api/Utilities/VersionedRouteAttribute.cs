namespace Bit.Api.Utilities;

/// <summary>
/// Marks an action to be served at a versioned route (e.g. /v2/...) instead of the
/// controller's default route prefix.  Used in conjunction with <see cref="VersionedRouteConvention"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class VersionedRouteAttribute : Attribute
{
    public int Version { get; }

    public VersionedRouteAttribute(int version)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        Version = version;
    }
}
