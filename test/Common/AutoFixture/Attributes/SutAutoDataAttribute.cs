using AutoFixture;

namespace Bit.Test.Common.AutoFixture.Attributes
{
    public class SutProviderCustomizeAttribute : BitCustomizeAttribute
    {
        public override ICustomization GetCustomization() => new SutProviderCustomization();
    }

    public class SutAutoDataAttribute : CustomAutoDataAttribute
    {
        public SutAutoDataAttribute(params Type[] iCustomizationTypes) : base(
            iCustomizationTypes.Append(typeof(SutProviderCustomization)).ToArray())
        { }
    }
}
