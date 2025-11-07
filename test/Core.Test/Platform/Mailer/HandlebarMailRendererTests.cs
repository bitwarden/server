using Bit.Core.Platform.Mail.Mailer;
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

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("../../../../malicious.txt")]
    [InlineData("../../malicious.txt")]
    [InlineData("../malicious.txt")]
    public async Task ReadSourceFromDiskAsync_PrevenetsPathTraversal_WhenMaliciousPathProvided(string maliciousPath)
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

            // Create a malicious file outside the template directory
            var maliciousFile = Path.Combine(Path.GetTempPath(), "malicious.txt");
            await File.WriteAllTextAsync(maliciousFile, "Malicious Content");

            var renderer = new HandlebarMailRenderer(logger, globalSettings);

            // Use reflection to call the private ReadSourceFromDiskAsync method
            var method = typeof(HandlebarMailRenderer).GetMethod("ReadSourceFromDiskAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task<string?>)method!.Invoke(renderer, new object[] { maliciousPath })!;
            var result = await task;

            // Should return null and not load the malicious file
            Assert.Null(result);

            // Verify that a warning was logged for the path traversal attempt
            logger.Received(1).Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>());

            // Cleanup malicious file
            if (File.Exists(maliciousFile))
            {
                File.Delete(maliciousFile);
            }
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

    [Fact]
    public async Task ReadSourceFromDiskAsync_AllowsValidFileWithDifferentCase_WhenCaseInsensitiveFileSystem()
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

            // Create a test template file
            var templateFileName = "TestTemplate.hbs";
            var templatePath = Path.Combine(tempDir, templateFileName);
            await File.WriteAllTextAsync(templatePath, "Test Content");

            var renderer = new HandlebarMailRenderer(logger, globalSettings);

            // Try to read with different case (should work on case-insensitive file systems like Windows/macOS)
            var method = typeof(HandlebarMailRenderer).GetMethod("ReadSourceFromDiskAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task<string?>)method!.Invoke(renderer, new object[] { templateFileName })!;
            var result = await task;

            // Should successfully read the file
            Assert.Equal("Test Content", result);

            // Verify no warning was logged
            logger.DidNotReceive().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>());
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
