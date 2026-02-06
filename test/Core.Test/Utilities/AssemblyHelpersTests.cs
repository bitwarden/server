using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class AssemblyHelpersTests
{
    [Fact]
    public void ReturnsValidVersionAndGitHash()
    {
        var version = AssemblyHelpers.GetVersion();
        _ = Version.Parse(version);

        var gitHash = AssemblyHelpers.GetGitHash();
        Assert.NotNull(gitHash);
        Assert.Equal(8, gitHash.Length);
    }
}
