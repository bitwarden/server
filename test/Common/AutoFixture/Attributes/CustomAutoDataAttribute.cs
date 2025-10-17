﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoFixture;
using AutoFixture.Xunit2;

namespace Bit.Test.Common.AutoFixture.Attributes;

public class CustomAutoDataAttribute : AutoDataAttribute
{
    public CustomAutoDataAttribute(params Type[] iCustomizationTypes) : this(iCustomizationTypes
        .Select(t => (ICustomization)Activator.CreateInstance(t)).ToArray())
    { }

    public CustomAutoDataAttribute(params ICustomization[] customizations) : base(() =>
    {
        var fixture = new Fixture().WithAutoNSubstitutions();
        foreach (var customization in customizations)
        {
            fixture.Customize(customization);
        }
        return fixture;
    })
    { }
}
