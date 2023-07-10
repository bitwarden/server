using AutoFixture;
using AutoFixture.AutoNSubstitute;

namespace Bit.Test.Common.AutoFixture.Attributes;

public class SutProviderCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new SutProviderCustomization();
}

public class AutoNSubstituteCustomizationAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new AutoNSubstituteCustomization();
}
