using System;
using System.Linq;
using AutoFixture;
using AutoFixture.Xunit2;

namespace Bit.Core.Test.AutoFixture.Attributes
{
    public class CustomAutoDataAttribute : AutoDataAttribute
    {
        public CustomAutoDataAttribute(params Type[] iCustomizationTypes) : this(iCustomizationTypes
            .Select(t => (ICustomization)Activator.CreateInstance(t)).ToArray())
        { }

        public CustomAutoDataAttribute(params ICustomization[] customizations) : base(() =>
        {
            var fixture = new Fixture();
            foreach (var customization in customizations)
            {
                fixture.Customize(customization);
            }
            return fixture;
        })
        { }
    }
}
