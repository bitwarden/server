using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Resources;

public class VerifyResources
{
    [Theory]
    [MemberData(nameof(GetResources))]
    public void Resource_FoundAndReadable(string resourceName)
    {
        var assembly = typeof(CoreHelpers).Assembly;

        using (var resource = assembly.GetManifestResourceStream(resourceName))
        {
            Assert.NotNull(resource);
            Assert.True(resource.CanRead);
        }
    }

    public static IEnumerable<object[]> GetResources()
    {
        yield return new[] { "Bit.Core.licensing.cer" };
        yield return new[] { "Bit.Core.MailTemplates.Handlebars.AddedCredit.html.hbs" };
        yield return new[] { "Bit.Core.MailTemplates.Handlebars.Layouts.Basic.html.hbs" };
    }
}
