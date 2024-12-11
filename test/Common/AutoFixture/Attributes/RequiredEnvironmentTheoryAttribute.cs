using Xunit;

namespace Bit.Test.Common.AutoFixture.Attributes;

/// <summary>
/// Used for requiring certain environment variables exist at the time. Mostly used for more edge unit tests that shouldn't
/// be run during CI builds or should only be ran in CI builds when pieces of information are available.
/// </summary>
public class RequiredEnvironmentTheoryAttribute : TheoryAttribute
{
    private readonly string[] _environmentVariableNames;

    public RequiredEnvironmentTheoryAttribute(params string[] environmentVariableNames)
    {
        _environmentVariableNames = environmentVariableNames;

        if (!HasRequiredEnvironmentVariables())
        {
            Skip =
                $"Missing one or more required environment variables. ({string.Join(", ", _environmentVariableNames)})";
        }
    }

    private bool HasRequiredEnvironmentVariables()
    {
        foreach (var env in _environmentVariableNames)
        {
            var value = Environment.GetEnvironmentVariable(env);

            if (value == null)
            {
                return false;
            }
        }

        return true;
    }
}
