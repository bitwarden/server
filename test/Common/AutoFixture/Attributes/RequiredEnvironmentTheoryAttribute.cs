using System;
using Xunit;

namespace Bit.Test.Common.AutoFixture.Attributes
{
    /// <summary>
    /// 
    /// </summary>
    public class RequiredEnvironmentTheoryAttribute : TheoryAttribute
    {
        private readonly bool _allowEmpty;
        private readonly string[] _environmentVariableNames;

        public RequiredEnvironmentTheoryAttribute(bool allowEmpty, params string[] environmentVariableNames)
        {
            _allowEmpty = allowEmpty;
            _environmentVariableNames = environmentVariableNames;

            if (!HasRequiredEnvironmentVariables())
            {
                Skip = $"Missing one or more required environment variables. ({string.Join(", ", _environmentVariableNames)})";
            }
        }

        public RequiredEnvironmentTheoryAttribute(params string[] environmentVariablesNames)
            : this(false, environmentVariablesNames)
        {

        }

        private bool HasRequiredEnvironmentVariables()
        {
            foreach (var env in _environmentVariableNames)
            {
                var value = Environment.GetEnvironmentVariable(env);

                if (value == null || (!_allowEmpty && value == ""))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
