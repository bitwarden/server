using Bit.Setup;
using NSubstitute;

namespace Setup.Test;

public class NginxConfigBuilderTests
{
    [Fact]
    public void TemplateModel_NoSendOrAttachmentConnectionStringLeavesCspUnchanged()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        var context = CreateContext(tempDir.FullName);
        context.Config.NginxHeaderContentSecurityPolicy =
            "default-src 'self'; connect-src 'self'; object-src 'none';";

        var model = new NginxConfigBuilder.TemplateModel(context);

        Assert.Equal(
            "default-src 'self'; connect-src 'self'; object-src 'none';",
            model.ContentSecurityPolicy);
    }

    [Fact]
    public void TemplateModel_AppendsAttachmentOriginWhenOnlyAttachmentIsConfigured()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        var context = CreateContext(tempDir.FullName);
        context.Config.Url = "https://vault.example.com";
        WriteConnectionStrings(tempDir.FullName, null,
            "DefaultEndpointsProtocol=https;AccountName=attachments;AccountKey=secret;EndpointSuffix=core.windows.net");

        var model = new NginxConfigBuilder.TemplateModel(context);

        Assert.Contains("https://attachments.blob.core.windows.net", model.ContentSecurityPolicy);
    }

    [Theory]
    [InlineData(
        "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=secret;EndpointSuffix=core.windows.net",
        "https://testaccount.blob.core.windows.net")]
    [InlineData(
        "DefaultEndpointsProtocol=http;AccountName=testaccount;AccountKey=secret;EndpointSuffix=core.usgovcloudapi.net",
        "http://testaccount.blob.core.usgovcloudapi.net")]
    [InlineData(
        "BlobEndpoint=https://blob.example.test:8443/storage;AccountName=testaccount;SharedAccessSignature=secret",
        "https://blob.example.test:8443")]
    public void TryGetSendBlobOrigin_DerivesOrUsesExplicitBlobEndpoint(string connectionString, string expected)
    {
        var tempDir = Directory.CreateTempSubdirectory();
        var context = CreateContext(tempDir.FullName);
        WriteSendConnectionString(tempDir.FullName, connectionString);

        Assert.True(NginxConfigBuilder.TryGetSendBlobOrigin(context, out var origin));
        Assert.Equal(expected, origin);
    }

    [Fact]
    public void TemplateModel_AppendsBlobOriginToDefaultConnectSrc()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        var context = CreateContext(tempDir.FullName);
        context.Config.Url = "https://vault.example.com";
        WriteSendConnectionString(tempDir.FullName,
            "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=secret;EndpointSuffix=core.windows.net");

        var model = new NginxConfigBuilder.TemplateModel(context);

        Assert.Contains("connect-src 'self' wss://vault.example.com https://api.pwnedpasswords.com " +
            "https://api.2fa.directory https://testaccount.blob.core.windows.net;", model.ContentSecurityPolicy);
    }

    [Fact]
    public void TemplateModel_PreservesCustomCspAndAvoidsDuplicateOrigin()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        var context = CreateContext(tempDir.FullName);
        context.Config.NginxHeaderContentSecurityPolicy =
            "default-src 'self'; connect-src 'self' https://testaccount.blob.core.windows.net; object-src 'none';";
        WriteSendConnectionString(tempDir.FullName,
            "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=secret;EndpointSuffix=core.windows.net");

        var model = new NginxConfigBuilder.TemplateModel(context);

        Assert.Equal(
            "default-src 'self'; connect-src 'self' https://testaccount.blob.core.windows.net; object-src 'none';",
            model.ContentSecurityPolicy);
    }

    [Fact]
    public void TemplateModel_InvalidConnectionStringLeavesCspUnchanged()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        var context = CreateContext(tempDir.FullName);
        context.Config.Url = "https://vault.example.com";
        context.Config.NginxHeaderContentSecurityPolicy =
            "default-src 'self'; connect-src 'self'; img-src 'self';";
        WriteSendConnectionString(tempDir.FullName, "AccountKey=secret");

        var model = new NginxConfigBuilder.TemplateModel(context);

        Assert.Equal(
            "default-src 'self'; connect-src 'self'; img-src 'self';",
            model.ContentSecurityPolicy);
    }

    [Fact]
    public void TemplateModel_RepeatedBuildIsIdentical()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        var context = CreateContext(tempDir.FullName);
        context.Config.Url = "https://vault.example.com";
        WriteSendConnectionString(tempDir.FullName,
            "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=secret;EndpointSuffix=core.windows.net");

        var first = new NginxConfigBuilder.TemplateModel(context).ContentSecurityPolicy;
        var second = new NginxConfigBuilder.TemplateModel(context).ContentSecurityPolicy;

        Assert.Equal(first, second);
    }

    [Fact]
    public void TemplateModel_AppendsDifferentSendAndAttachmentOrigins()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        var context = CreateContext(tempDir.FullName);
        context.Config.Url = "https://vault.example.com";
        WriteConnectionStrings(tempDir.FullName,
            "DefaultEndpointsProtocol=https;AccountName=sendaccount;AccountKey=secret;EndpointSuffix=core.windows.net",
            "DefaultEndpointsProtocol=https;AccountName=attachmentaccount;AccountKey=secret;EndpointSuffix=core.windows.net");

        var model = new NginxConfigBuilder.TemplateModel(context);

        Assert.Contains("https://sendaccount.blob.core.windows.net", model.ContentSecurityPolicy);
        Assert.Contains("https://attachmentaccount.blob.core.windows.net", model.ContentSecurityPolicy);
    }

    [Fact]
    public void TemplateModel_AddsOnlyOneOriginForMatchingConnectionStrings()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        var context = CreateContext(tempDir.FullName);
        context.Config.Url = "https://vault.example.com";
        const string connectionString =
            "DefaultEndpointsProtocol=https;AccountName=sharedaccount;AccountKey=secret;EndpointSuffix=core.windows.net";
        WriteConnectionStrings(tempDir.FullName, connectionString, connectionString);

        var model = new NginxConfigBuilder.TemplateModel(context);

        Assert.Equal(1, model.ContentSecurityPolicy.Split("https://sharedaccount.blob.core.windows.net").Length - 1);
    }

    [Fact]
    public void AppendConnectSrcOrigin_AddsDirectiveWhenCustomCspHasNoConnectSrc()
    {
        var result = NginxConfigBuilder.AppendConnectSrcOrigin(
            "default-src 'self'; object-src 'none';", "https://testaccount.blob.core.windows.net");

        Assert.Equal(
            "default-src 'self'; object-src 'none'; connect-src https://testaccount.blob.core.windows.net;",
            result);
    }

    private static Context CreateContext(string rootDirectory)
    {
        var app = Substitute.For<Application>();
        app.RootDirectory.Returns(rootDirectory);
        Environment.SetEnvironmentVariable("globalSettings__send__connectionString", null);
        Environment.SetEnvironmentVariable("globalSettings__attachment__connectionString", null);
        return new Context
        {
            App = app,
            Quiet = true,
            Parameters = new Dictionary<string, string>(),
        };
    }

    private static void WriteSendConnectionString(string rootDirectory, string connectionString)
    {
        var envDirectory = Path.Combine(rootDirectory, "env");
        Directory.CreateDirectory(envDirectory);
        File.WriteAllText(
            Path.Combine(envDirectory, "global.override.env"),
            $"globalSettings__send__connectionString={connectionString}{Environment.NewLine}");
    }

    private static void WriteConnectionStrings(string rootDirectory, string? sendConnectionString,
        string? attachmentConnectionString)
    {
        var envDirectory = Path.Combine(rootDirectory, "env");
        Directory.CreateDirectory(envDirectory);
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(sendConnectionString))
        {
            lines.Add($"globalSettings__send__connectionString={sendConnectionString}");
        }
        if (!string.IsNullOrWhiteSpace(attachmentConnectionString))
        {
            lines.Add($"globalSettings__attachment__connectionString={attachmentConnectionString}");
        }
        File.WriteAllLines(Path.Combine(envDirectory, "global.override.env"), lines);
    }
}
