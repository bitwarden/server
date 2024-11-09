using AutoFixture;

namespace Bit.Test.Common.AutoFixture.Attributes;

public class SutProviderCustomizeAttribute(bool create = true) : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new SutProviderCustomization(create);
}
