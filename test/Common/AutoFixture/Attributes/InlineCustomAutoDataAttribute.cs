using AutoFixture.Xunit2;
using Xunit;
using Xunit.Sdk;

namespace Bit.Test.Common.AutoFixture.Attributes;

public class InlineCustomAutoDataAttribute : CompositeDataAttribute
{
    public InlineCustomAutoDataAttribute(Type[] iCustomizationTypes, params object[] values)
        : base(
            new DataAttribute[]
            {
                new InlineDataAttribute(values),
                new CustomAutoDataAttribute(iCustomizationTypes),
            }
        ) { }
}
