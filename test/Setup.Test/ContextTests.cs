using Bit.Setup;
using NSubstitute;

namespace Setup.Test;

public class ContextTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory();
    private readonly Context _sut;

    public ContextTests()
    {
        var app = Substitute.For<Application>();
        app.RootDirectory.Returns(_dir.FullName);
        _sut = new Context { App = app };
    }

    public void Dispose() => _dir.Delete(true);

    private string ConfigPath => Path.Combine(_dir.FullName, "config.yml");

    [Fact]
    public void SaveConfiguration_Defaults_WritesCommentedUnderscoredKeys()
    {
        _sut.SaveConfiguration();

        var lines = File.ReadAllLines(ConfigPath);
        Assert.Contains("url: https://localhost", lines);
        Assert.Contains("ssl_managed_lets_encrypt: false", lines);
        Assert.Contains("enable_built_in_ms_sql: true", lines);
        Assert.Contains("ssl_versions: ", lines);
        Assert.DoesNotContain(lines, l => l.StartsWith("domain:"));
        Assert.Contains("# Configure Nginx for SSL.", lines);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValuesWithSpecialCharacters()
    {
        _sut.Config.SslVersions = "TLSv1.2 TLSv1.3";
        _sut.Config.NginxHeaderContentSecurityPolicy = "default-src 'self'; x: y # z \"q\"";
        _sut.Config.RealIps = new List<string> { "10.10.0.0/24", "172.16.0.0/16" };
        _sut.Config.EnableScim = true;
        _sut.SaveConfiguration();

        _sut.Config = new Configuration();
        _sut.LoadConfiguration();

        Assert.Equal("TLSv1.2 TLSv1.3", _sut.Config.SslVersions);
        Assert.Equal("default-src 'self'; x: y # z \"q\"", _sut.Config.NginxHeaderContentSecurityPolicy);
        Assert.Equal(new[] { "10.10.0.0/24", "172.16.0.0/16" }, _sut.Config.RealIps);
        Assert.True(_sut.Config.EnableScim);
        Assert.Null(_sut.Config.SslCiphersuites);
    }

    [Fact]
    public void LoadConfiguration_HandWrittenYaml_ParsesQuotingFlowListsCommentsAndUnknownKeys()
    {
        File.WriteAllText(ConfigPath, """
            # comment
            url: "https://example.com"   # trailing comment
            http_port: '8080'
            https_port: 8443
            ssl: false
            real_ips: ['10.0.0.0/8', "192.168.0.0/16"]
            unknown_key: ignored
            """);

        _sut.LoadConfiguration();

        Assert.Equal("https://example.com", _sut.Config.Url);
        Assert.Equal("8080", _sut.Config.HttpPort);
        Assert.Equal("8443", _sut.Config.HttpsPort);
        Assert.False(_sut.Config.Ssl);
        Assert.Equal(new[] { "10.0.0.0/8", "192.168.0.0/16" }, _sut.Config.RealIps);
        Assert.True(_sut.Config.GenerateComposeConfig);
    }
}
