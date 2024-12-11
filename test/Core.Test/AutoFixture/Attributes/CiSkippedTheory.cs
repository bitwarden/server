namespace Bit.Core.Test.AutoFixture.Attributes;

public sealed class CiSkippedTheory : Xunit.TheoryAttribute
{
    private static bool IsGithubActions() => Environment.GetEnvironmentVariable("CI") != null;

    public CiSkippedTheory()
    {
        if (IsGithubActions())
        {
            Skip = "Ignore during CI builds";
        }
    }
}
