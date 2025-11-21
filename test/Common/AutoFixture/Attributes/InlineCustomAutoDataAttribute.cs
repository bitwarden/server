using AutoFixture.Xunit3;
using Xunit;

namespace Bit.Test.Common.AutoFixture.Attributes;

public class InlineCustomAutoDataAttribute : CompositeDataAttribute
{
    public InlineCustomAutoDataAttribute(Type[] iCustomizationTypes, params object[] values) : base([
        new InlineDataAttribute(values),
        new CustomAutoDataAttribute(iCustomizationTypes)
    ])
    { }
}
