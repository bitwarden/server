namespace Bit.Core.Billing.Licenses.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class LicenseVersionAttribute(int version) : Attribute
{

    /// <summary>
    /// The license version in which this property was added.
    /// </summary>
    public int Version { get; } = version;
}
