#nullable enable

using System.ComponentModel;
using System.Reflection;
using AutoFixture;
using AutoFixture.Kernel;
using AutoFixture.Xunit2;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Test.Common.Helpers;

public static class BitAutoDataAttributeHelpers
{
    public static IEnumerable<object?[]> GetData(MethodInfo testMethod, IFixture fixture, object?[] fixedTestParameters)
    {
        var methodParameters = testMethod.GetParameters();
        // We aren't worried about a test method not having a class it belongs to.
        var classCustomizations = testMethod.DeclaringType!.GetCustomAttributes<BitCustomizeAttribute>().Select(attr => attr.GetCustomization());
        var methodCustomizations = testMethod.GetCustomAttributes<BitCustomizeAttribute>().Select(attr => attr.GetCustomization());

        fixedTestParameters ??= Array.Empty<object>();

        fixture = ApplyCustomizations(ApplyCustomizations(fixture, classCustomizations), methodCustomizations);

        // The first n number of parameters should be match to the supplied parameters
        var fixedTestInputParameters = methodParameters.Take(fixedTestParameters.Length).Zip(fixedTestParameters);

        var missingParameters = methodParameters.Skip(fixedTestParameters.Length).Select(p => CustomizeAndCreate(p, fixture));

        return new object?[1][] { ConvertFixedParameters(fixedTestInputParameters.ToArray()).Concat(missingParameters).ToArray() };
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

    public static IEnumerable<object?> ConvertFixedParameters((ParameterInfo Parameter, object? Value)[] fixedParameters)
    {
        var output = new object?[fixedParameters.Length];
        for (var i = 0; i < fixedParameters.Length; i++)
        {
            var (parameter, value) = fixedParameters[i];
            // If the value is null, just return the value
            if (value is null || value.GetType() == parameter.ParameterType)
            {
                output[i] = value;
                continue;
            }

            // If the value is a string and it's not a perfect match, try to convert it.
            if (value is string stringValue)
            {
                // 
                if (parameter.ParameterType.IsGenericType && parameter.ParameterType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    if (TryConvertToType(stringValue, Nullable.GetUnderlyingType(parameter.ParameterType)!, out var nullableConvertedValue))
                    {
                        output[i] = nullableConvertedValue;
                        continue;
                    }

                    // We couldn't convert it, so set it as the input value and let XUnit throw
                    output[i] = value;
                    continue;
                }

                if (TryConvertToType(stringValue, parameter.ParameterType, out var convertedValue))
                {
                    output[i] = convertedValue;
                    continue;
                }

                // We couldn't convert it, so set it as the input value and let XUnit throw
                output[i] = value;
            }

            // No easy conversion, give them back the value
            output[i] = value;
        }

        return output;
    }

    private static bool TryConvertToType(string value, Type destinationType, out object? convertedValue)
    {
        convertedValue = null;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var converter = TypeDescriptor.GetConverter(destinationType);

        if (converter.CanConvertFrom(typeof(string)))
        {
            convertedValue = converter.ConvertFromInvariantString(value);
            return true;
        }

        return false;
    }
}
