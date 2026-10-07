using System.Net;
using System.Runtime.CompilerServices;

namespace Bit.Server.IntegrationTest;

public class ServerTests
{
    [Fact]
    public async Task AttachmentsStyleUse()
    {
        using var tempDir = new TempDir();

        await tempDir.WriteAsync("my-file.txt", "Hello!");

        using var server = new Server
        {
            ContentRoot = tempDir.Info.FullName,
            WebRoot = ".",
            ServeUnknown = true,
        };

        var client = server.CreateClient();

        var response = await client.GetAsync("/my-file.txt", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Hello!", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WebVaultStyleUse()
    {
        using var tempDir = new TempDir();

        await tempDir.WriteAsync("index.html", "<html></html>");
        await tempDir.WriteAsync(Path.Join("app", "file.js"), "AppStuff");
        await tempDir.WriteAsync(Path.Join("locales", "file.json"), "LocalesStuff");
        await tempDir.WriteAsync(Path.Join("fonts", "file.ttf"), "FontsStuff");
        await tempDir.WriteAsync(Path.Join("connectors", "file.js"), "ConnectorsStuff");
        await tempDir.WriteAsync(Path.Join("scripts", "file.js"), "ScriptsStuff");
        await tempDir.WriteAsync(Path.Join("images", "file.avif"), "ImagesStuff");
        await tempDir.WriteAsync(Path.Join("test", "file.json"), "{}");

        using var server = new Server
        {
            ContentRoot = tempDir.Info.FullName,
            WebRoot = ".",
            ServeUnknown = false,
            WebVault = true,
            AppIdLocation = Path.Join(tempDir.Info.FullName, "test", "file.json"),
        };

        var client = server.CreateClient();

        // Going to root should return the default file
        var response = await client.GetAsync("", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("<html></html>", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        // No caching on the default document
        Assert.Null(response.Headers.CacheControl?.MaxAge);

        await ExpectMaxAgeAsync("app/file.js", TimeSpan.FromDays(14));
        await ExpectMaxAgeAsync("locales/file.json", TimeSpan.FromDays(14));
        await ExpectMaxAgeAsync("fonts/file.ttf", TimeSpan.FromDays(14));
        await ExpectMaxAgeAsync("connectors/file.js", TimeSpan.FromDays(14));
        await ExpectMaxAgeAsync("scripts/file.js", TimeSpan.FromDays(14));
        await ExpectMaxAgeAsync("images/file.avif", TimeSpan.FromDays(7));

        response = await client.GetAsync("app-id.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        async Task ExpectMaxAgeAsync(string path, TimeSpan maxAge)
        {
            response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(response.Headers.CacheControl);
            Assert.Equal(maxAge, response.Headers.CacheControl.MaxAge);
        }
    }

    private class TempDir([CallerMemberName] string test = null!) : IDisposable
    {
        public DirectoryInfo Info { get; } = Directory.CreateTempSubdirectory(test);

        public void Dispose()
        {
            Info.Delete(recursive: true);
        }

        public async Task WriteAsync(string fileName, string content)
        {
            var fullPath = Path.Join(Info.FullName, fileName);
            var directory = Path.GetDirectoryName(fullPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, content, TestContext.Current.CancellationToken);
        }
    }
}
