using AutoFixture;

namespace Bit.Test.Common.AutoFixture.Attributes;

/// <summary>
/// <para>
///     Base class for customizing parameters in methods decorated with the
///     Bit.Test.Common.AutoFixture.Attributes.MemberAutoDataAttribute.
/// </para>
/// ⚠ Warning ⚠ Will not insert customizations into AutoFixture's AutoDataAttribute build chain
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Parameter,
    AllowMultiple = true
)]
public abstract class BitCustomizeAttribute : Attribute
{
    /// <summary>
    /// Gets a customization for the method's parameters.
    /// </summary>
    /// <returns>A customization for the method's parameters.</returns>
    public abstract ICustomization GetCustomization();
}
