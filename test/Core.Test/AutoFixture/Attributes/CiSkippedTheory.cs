using System;
using System.Linq;
using AutoFixture;
using AutoFixture.Xunit2;

namespace Bit.Core.Test.AutoFixture.Attributes
{
    public sealed class CiSkippedTheory : Xunit.TheoryAttribute
    {
        public CiSkippedTheory() {
            if(IsGithubActions()) {
                Skip = "Ignore during CI builds";
            }
    }
    
    private static bool IsGithubActions()
        => Environment.GetEnvironmentVariable("GITHUB_CONTEXT") != null;   
    }
}
