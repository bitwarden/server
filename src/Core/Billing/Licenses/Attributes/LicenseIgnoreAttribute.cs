namespace Bit.Core.Billing.Licenses.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class LicenseIgnoreAttribute(bool includeInSignature = false) : Attribute
{

    /// <summary>
    /// If true, the property will be included when computing the license signature, but ignored for other operations.
    /// If false, the property will be completely ignored including for signature computation.
    /// </summary>
    public bool IncludeInSignature { get; } = includeInSignature;
}
