namespace Bit.Core.Billing.Attributes;

/// <summary>
/// If this attribute is applied to a class member, it will be ignored when encoding the license data
/// </summary>
public class LicenseIgnoreAttribute : Attribute
{
    public LicenseIgnoreCondition Condition { get; set; } = LicenseIgnoreCondition.Always;
}

public enum LicenseIgnoreCondition
{
    /// <summary>
    /// Always ignore the property
    /// </summary>
    Always = 0,

    /// <summary>
    /// Ignore the property if the hash is being computed
    /// </summary>
    OnHash = 1,

    /// <summary>
    /// Ignore the property if the signature is being computed
    /// </summary>
    OnSignature = 2
}
