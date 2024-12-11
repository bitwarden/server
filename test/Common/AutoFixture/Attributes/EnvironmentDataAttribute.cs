using System.Reflection;
using Xunit.Sdk;

namespace Bit.Test.Common.AutoFixture.Attributes;

/// <summary>
/// Used for collecting data from environment useful for when we want to test an integration with another service and
/// it might require an api key or other piece of sensitive data that we don't want slipping into the wrong hands.
/// </summary>
/// <remarks>
/// It probably should be refactored to support fixtures and other customization so it can more easily be used in conjunction
/// with more parameters.  Currently it attempt to match environment variable names to values of the parameter type in that positions.
/// It will start from the first parameter and go for each supplied name.
/// </remarks>
public class EnvironmentDataAttribute : DataAttribute
{
    private readonly string[] _environmentVariableNames;

    public EnvironmentDataAttribute(params string[] environmentVariableNames)
    {
        _environmentVariableNames = environmentVariableNames;
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var methodParameters = testMethod.GetParameters();

        if (methodParameters.Length < _environmentVariableNames.Length)
        {
            throw new ArgumentException(
                $"The target test method only has {methodParameters.Length} arguments but you supplied {_environmentVariableNames.Length}"
            );
        }

        var values = new object[_environmentVariableNames.Length];

        for (var i = 0; i < _environmentVariableNames.Length; i++)
        {
            values[i] = Convert.ChangeType(
                Environment.GetEnvironmentVariable(_environmentVariableNames[i]),
                methodParameters[i].ParameterType
            );
        }

        return new[] { values };
    }
}
