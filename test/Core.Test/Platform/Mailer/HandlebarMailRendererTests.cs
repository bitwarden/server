using Bit.Core.Platform.Mailer;
using Bit.Core.Test.Platform.Mailer.TestMail;
using Xunit;

namespace Bit.Core.Test.Platform.Mailer;

public class HandlebarMailRendererTests
{
    [Fact]
    public async Task RenderAsync_ReturnsExpectedHtmlAndTxt()
    {
        var renderer = new HandlebarMailRenderer();
        var view = new TestMailView { Name = "John Smith" };

        var (html, txt) = await renderer.RenderAsync(view);

        Assert.Equal("Hello <b>John Smith</b>", html.Trim());
        Assert.Equal("Hello John Smith", txt.Trim());
    }
}
