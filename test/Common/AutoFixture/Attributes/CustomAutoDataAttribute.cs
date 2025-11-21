using AutoFixture;
using AutoFixture.Xunit3;

namespace Bit.Test.Common.AutoFixture.Attributes;

public class CustomAutoDataAttribute : AutoDataAttribute
{
    public CustomAutoDataAttribute(params Type[] iCustomizationTypes) : this(CreateCustomizations(iCustomizationTypes))
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

    private static ICustomization[] CreateCustomizations(Type[] customizationTypes)
    {
        var customizations = new ICustomization[customizationTypes.Length];
        for (var i = 0; i < customizationTypes.Length; i++)
        {
            var customizationType = customizationTypes[i];
            var customizationObj = Activator.CreateInstance(customizationTypes[i]);
            if (customizationObj is not ICustomization customization)
            {
                throw new InvalidOperationException($"{customizationType.FullName} should implement ICustomization");
            }

            customizations[i] = customization;
        }

        return customizations;
    }
}
