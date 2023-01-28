using AutoFixture;

namespace Bit.Test.Common.AutoFixture.Attributes;

public class SutProviderCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new SutProviderCustomization();
}
