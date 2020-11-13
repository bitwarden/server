using System;
using AutoFixture;
using AutoFixture.Xunit2;

namespace Bit.Core.Test.AutoFixture.Attributes
{
    internal class CustomAutoDataAttribute : AutoDataAttribute
    {
        public CustomAutoDataAttribute(params Type[] iCustomizationTypes)
        : base(() =>
        {
            var fixture = new Fixture();
            foreach (Type iCustomizationType in iCustomizationTypes)
            {
                fixture.Customize((ICustomization)Activator.CreateInstance(iCustomizationType));
            }
            return fixture;
        })
        { }
    }
}
