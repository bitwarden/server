using Xunit;

namespace Bit.Icons.Test.Resources;

public class VerifyResources
{
    [Theory]
    [InlineData("Bit.Icons.Resources.public_suffix_list.dat")]
    public void Resources_FoundAndReadable(string resourceName)
    {
        var assembly = typeof(Program).Assembly;

        using (var resource = assembly.GetManifestResourceStream(resourceName))
        {
            Assert.NotNull(resource);
            Assert.True(resource.CanRead);
        }
    }
}
