using System;

namespace Bit.Test.Common.Helpers
{
    public sealed class CiSkippedTheoryAttribute : Xunit.TheoryAttribute
    {
        private static bool IsGithubActions() => Environment.GetEnvironmentVariable("CI") != null;
        public CiSkippedTheoryAttribute()
        {
            if (IsGithubActions())
            {
                Skip = "Ignore during CI builds";
            }
        }
    }
}
