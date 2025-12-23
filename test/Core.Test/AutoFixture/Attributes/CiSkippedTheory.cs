using System.Runtime.CompilerServices;

namespace Bit.Core.Test.AutoFixture.Attributes;

public sealed class CiSkippedTheory : Xunit.TheoryAttribute
{
    private static bool IsGithubActions() => Environment.GetEnvironmentVariable("CI") != null;

    public CiSkippedTheory([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1) : base(sourceFilePath, sourceLineNumber)
    {
        if (IsGithubActions())
        {
            Skip = "Ignore during CI builds";
        }
    }
}
