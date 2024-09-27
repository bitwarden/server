namespace Bit.Core.Billing.Attributes;

/// <summary>
/// If this attribute is applied to a class member, it will be included when encoding the license if the license version
/// is equal or greater than the specified version.
/// </summary>
/// <param name="version"></param>
public class LicenseVersionAttribute(int version) : Attribute
{
    public int Version { get; set; } = version;
}
