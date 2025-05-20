namespace Bit.Core.Billing.Licenses.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class LicenseIgnoreAttribute(bool includeInHash = true) : Attribute
{

    /// <summary>
    /// If true, the property will be included when computing the license hash, but ignored for other operations.
    /// If false, the property will be completely ignored including for hash computation.
    /// </summary>
    public bool IncludeInHash { get; } = includeInHash;
}
