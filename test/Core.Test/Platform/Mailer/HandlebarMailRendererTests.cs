using Bit.Core.Platform.Mailer;
using Bit.Core.Settings;
using Bit.Core.Test.Platform.Mailer.TestMail;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Platform.Mailer;

public class HandlebarMailRendererTests
{
    [Fact]
    public async Task RenderAsync_ReturnsExpectedHtmlAndTxt()
    {
        var logger = Substitute.For<ILogger<HandlebarMailRenderer>>();
        var globalSettings = new GlobalSettings { SelfHosted = false };
        var renderer = new HandlebarMailRenderer(logger, globalSettings);

        var view = new TestMailView { Name = "John Smith" };

        var (html, txt) = await renderer.RenderAsync(view);

        Assert.Equal("Hello <b>John Smith</b>", html.Trim());
        Assert.Equal("Hello John Smith", txt.Trim());
    }

    [Fact]
    public async Task RenderAsync_LoadsFromDisk_WhenSelfHostedAndFileExists()
    {
        var logger = Substitute.For<ILogger<HandlebarMailRenderer>>();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var globalSettings = new GlobalSettings
            {
                SelfHosted = true,
                MailTemplateDirectory = tempDir
            };

            // Create test template files on disk
            var htmlTemplatePath = Path.Combine(tempDir, "Bit.Core.Test.Platform.Mailer.TestMail.TestMailView.html.hbs");
            var txtTemplatePath = Path.Combine(tempDir, "Bit.Core.Test.Platform.Mailer.TestMail.TestMailView.text.hbs");
            await File.WriteAllTextAsync(htmlTemplatePath, "Custom HTML: <b>{{Name}}</b>");
            await File.WriteAllTextAsync(txtTemplatePath, "Custom TXT: {{Name}}");

            var renderer = new HandlebarMailRenderer(logger, globalSettings);
            var view = new TestMailView { Name = "Jane Doe" };

            var (html, txt) = await renderer.RenderAsync(view);

            Assert.Equal("Custom HTML: <b>Jane Doe</b>", html.Trim());
            Assert.Equal("Custom TXT: Jane Doe", txt.Trim());
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
