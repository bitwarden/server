using System;
using System.Linq;
using AutoFixture;

namespace Bit.Test.Common.AutoFixture.Attributes
{
    public class InlineSutAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineSutAutoDataAttribute(params object[] values) : base(
            new Type[] { typeof(SutProviderCustomization) }, values)
        { }
        public InlineSutAutoDataAttribute(Type[] iCustomizationTypes, params object[] values) : base(
            iCustomizationTypes.Append(typeof(SutProviderCustomization)).ToArray(), values)
        { }

        public InlineSutAutoDataAttribute(ICustomization[] customizations, params object[] values) : base(
            customizations.Append(new SutProviderCustomization()).ToArray(), values)
        { }
    }
}
