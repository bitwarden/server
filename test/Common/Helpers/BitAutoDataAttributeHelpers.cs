using System.Reflection;
using AutoFixture;
using AutoFixture.Kernel;
using AutoFixture.Xunit2;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Test.Common.Helpers;

public static class BitAutoDataAttributeHelpers
{
    public static IEnumerable<object[]> GetData(MethodInfo testMethod, IFixture fixture, object[] fixedTestParameters)
    {
        var methodParameters = testMethod.GetParameters();
        var classCustomizations = testMethod.DeclaringType.GetCustomAttributes<BitCustomizeAttribute>().Select(attr => attr.GetCustomization());
        var methodCustomizations = testMethod.GetCustomAttributes<BitCustomizeAttribute>().Select(attr => attr.GetCustomization());

        fixedTestParameters = fixedTestParameters ?? Array.Empty<object>();

        fixture = ApplyCustomizations(ApplyCustomizations(fixture, classCustomizations), methodCustomizations);
        var missingParameters = methodParameters.Skip(fixedTestParameters.Length).Select(p => CustomizeAndCreate(p, fixture));

        return new object[1][] { fixedTestParameters.Concat(missingParameters).ToArray() };
    }

    public static object CustomizeAndCreate(ParameterInfo p, IFixture fixture)
    {
        var customizations = p.GetCustomAttributes(typeof(CustomizeAttribute), false)
            .OfType<CustomizeAttribute>()
            .Select(attr => attr.GetCustomization(p));

        var context = new SpecimenContext(ApplyCustomizations(fixture, customizations));
        return context.Resolve(p);
    }

    public static IFixture ApplyCustomizations(IFixture fixture, IEnumerable<ICustomization> customizations)
    {
        var newFixture = new Fixture();

        foreach (var customization in fixture.Customizations.Reverse().Select(b => b.ToCustomization()))
        {
            newFixture.Customize(customization);
        }

        foreach (var customization in customizations)
        {
            newFixture.Customize(customization);
        }

        return newFixture;
    }
}
