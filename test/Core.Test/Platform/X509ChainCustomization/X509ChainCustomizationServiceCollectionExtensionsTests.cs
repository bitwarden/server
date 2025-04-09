using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Bit.Core.Platform.X509ChainCustomization;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Platform.X509ChainCustomization;

public class X509ChainCustomizationServiceCollectionExtensionsTests
{
    private static X509Certificate2 CreateSelfSignedCert(string commonName)
    {
        using var rsa = RSA.Create(2048);
        var certRequest = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return certRequest.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
    }

    [Fact]
    public async Task OptionsPatternReturnsCachedValue()
    {
        var tempDir = Directory.CreateTempSubdirectory("certs");

        var tempCert = Path.Combine(tempDir.FullName, "test.crt");
        await File.WriteAllBytesAsync(tempCert, CreateSelfSignedCert("localhost").Export(X509ContentType.Cert));

        var services = CreateServices((gs, environment, config) =>
        {
            config["X509ChainOptions:AdditionalCustomTrustCertificatesDirectory"] = tempDir.FullName;
        });

        // Create options once
        var firstOptions = services.GetRequiredService<IOptions<X509ChainOptions>>().Value;

        Assert.NotNull(firstOptions.AdditionalCustomTrustCertificates);
        var cert = Assert.Single(firstOptions.AdditionalCustomTrustCertificates);
        Assert.Equal("CN=localhost", cert.Subject);

        // Since the second resolution should have cached values, deleting the file during operation
        // should have no impact.
        File.Delete(tempCert);

        // This is expected to be a cached version and doesn't actually need to go and read the file system
        var secondOptions = services.GetRequiredService<IOptions<X509ChainOptions>>().Value;
        Assert.Same(firstOptions, secondOptions);

        // This is the same reference as the first one so it shouldn't be different but just in case.
        Assert.NotNull(secondOptions.AdditionalCustomTrustCertificates);
        Assert.Single(secondOptions.AdditionalCustomTrustCertificates);
    }

    [Fact]
    public async Task DoesNotProvideCustomCallbackOnCloud()
    {
        var tempDir = Directory.CreateTempSubdirectory("certs");

        var tempCert = Path.Combine(tempDir.FullName, "test.crt");
        await File.WriteAllBytesAsync(tempCert, CreateSelfSignedCert("localhost").Export(X509ContentType.Cert));

        var options = CreateOptions((gs, environment, config) =>
        {
            gs.SelfHosted = false;

            config["X509ChainOptions:AdditionalCustomTrustCertificatesDirectory"] = tempDir.FullName;
        });

        Assert.False(options.TryGetCustomRemoteCertificateValidationCallback(out _));
    }

    [Fact]
    public async Task ManuallyAddingOptionsTakesPrecedence()
    {
        var tempDir = Directory.CreateTempSubdirectory("certs");

        var tempCert = Path.Combine(tempDir.FullName, "test.crt");
        await File.WriteAllBytesAsync(tempCert, CreateSelfSignedCert("localhost").Export(X509ContentType.Cert));

        var services = CreateServices((gs, environment, config) =>
        {
            config["X509ChainOptions:AdditionalCustomTrustCertificatesDirectory"] = tempDir.FullName;
        }, services =>
        {
            services.Configure<X509ChainOptions>(options =>
            {
                options.AdditionalCustomTrustCertificates = [CreateSelfSignedCert("example.com")];
            });
        });

        var options = services.GetRequiredService<IOptions<X509ChainOptions>>().Value;

        Assert.True(options.TryGetCustomRemoteCertificateValidationCallback(out var callback));
        var cert = Assert.Single(options.AdditionalCustomTrustCertificates);
        Assert.Equal("CN=example.com", cert.Subject);

        var fakeLogCollector = services.GetFakeLogCollector();

        Assert.Contains(fakeLogCollector.GetSnapshot(),
            r => r.Message == $"Additional custom trust certificates were added directly, skipping loading them from '{tempDir}'");
    }

    [Fact]
    public void NullCustomDirectory_SkipsTryingToLoad()
    {
        var services = CreateServices((gs, environment, config) =>
        {
            config["X509ChainOptions:AdditionalCustomTrustCertificatesDirectory"] = null;
        });

        var options = services.GetRequiredService<IOptions<X509ChainOptions>>().Value;

        Assert.False(options.TryGetCustomRemoteCertificateValidationCallback(out _));
    }

    [Theory]
    [InlineData("Development", LogLevel.Debug)]
    [InlineData("Production", LogLevel.Warning)]
    public void CustomDirectoryDoesNotExist_Logs(string environment, LogLevel logLevel)
    {
        var fakeDir = "/fake/dir/that/does/not/exist";
        var services = CreateServices((gs, hostEnvironment, config) =>
        {
            hostEnvironment.EnvironmentName = environment;

            config["X509ChainOptions:AdditionalCustomTrustCertificatesDirectory"] = fakeDir;
        });

        var options = services.GetRequiredService<IOptions<X509ChainOptions>>().Value;

        Assert.False(options.TryGetCustomRemoteCertificateValidationCallback(out _));

        var fakeLogCollector = services.GetFakeLogCollector();

        Assert.Contains(fakeLogCollector.GetSnapshot(),
            r => r.Message == $"An additional custom trust certificate directory was given '{fakeDir}' but that directory does not exist."
                && r.Level == logLevel
        );
    }

    [Fact]
    public async Task NamedOptions_NotConfiguredAsync()
    {
        // To help make sure this fails for the right reason we should add certs to the directory
        var tempDir = Directory.CreateTempSubdirectory("certs");

        var tempCert = Path.Combine(tempDir.FullName, "test.crt");
        await File.WriteAllBytesAsync(tempCert, CreateSelfSignedCert("localhost").Export(X509ContentType.Cert));

        var services = CreateServices((gs, environment, config) =>
        {
            config["X509ChainOptions:AdditionalCustomTrustCertificatesDirectory"] = tempDir.FullName;
        });

        var options = services.GetRequiredService<IOptionsMonitor<X509ChainOptions>>();

        var namedOptions = options.Get("SomeName");

        Assert.Null(namedOptions.AdditionalCustomTrustCertificates);
    }

    [Fact]
    public void CustomLocation_NoCertificates_Logs()
    {
        var tempDir = Directory.CreateTempSubdirectory("certs");
        var services = CreateServices((gs, hostEnvironment, config) =>
        {
            config["X509ChainOptions:AdditionalCustomTrustCertificatesDirectory"] = tempDir.FullName;
        });

        var options = services.GetRequiredService<IOptions<X509ChainOptions>>().Value;

        Assert.False(options.TryGetCustomRemoteCertificateValidationCallback(out _));

        var fakeLogCollector = services.GetFakeLogCollector();

        Assert.Contains(fakeLogCollector.GetSnapshot(),
            r => r.Message == $"No additional custom trust certificates were found in '{tempDir.FullName}'"
        );
    }

    [Fact]
    public async Task CallHttpWithSelfSignedCert_SelfSignedCertificateConfigured_Works()
    {
        var selfSignedCertificate = CreateSelfSignedCert("localhost");
        await using var app = await CreateServerAsync(55555, options =>
        {
            options.ServerCertificate = selfSignedCertificate;
        });

        var services = CreateServices((gs, environment, config) => { }, services =>
        {
            services.Configure<X509ChainOptions>(options =>
            {
                options.AdditionalCustomTrustCertificates = [selfSignedCertificate];
            });
        });

        var httpClient = services.GetRequiredService<IHttpClientFactory>().CreateClient();

        var response = await httpClient.GetStringAsync("https://localhost:55555");
        Assert.Equal("Hi", response);
    }

    [Fact]
    public async Task CallHttpWithSelfSignedCert_SelfSignedCertificateNotConfigured_Throws()
    {
        var selfSignedCertificate = CreateSelfSignedCert("localhost");
        await using var app = await CreateServerAsync(55556, options =>
        {
            options.ServerCertificate = selfSignedCertificate;
        });

        var services = CreateServices((gs, environment, config) => { }, services =>
        {
            services.Configure<X509ChainOptions>(options =>
            {
                options.AdditionalCustomTrustCertificates = [CreateSelfSignedCert("example.com")];
            });
        });

        var httpClient = services.GetRequiredService<IHttpClientFactory>().CreateClient();

        var requestException = await Assert.ThrowsAsync<HttpRequestException>(async () => await httpClient.GetStringAsync("https://localhost:55556"));
        Assert.NotNull(requestException.InnerException);
        var authenticationException = Assert.IsAssignableFrom<AuthenticationException>(requestException.InnerException);
        Assert.Equal("The remote certificate was rejected by the provided RemoteCertificateValidationCallback.", authenticationException.Message);
    }

    [Fact]
    public async Task CallHttpWithSelfSignedCert_SelfSignedCertificateConfigured_WithExtraCert_Works()
    {
        var selfSignedCertificate = CreateSelfSignedCert("localhost");
        await using var app = await CreateServerAsync(55557, options =>
        {
            options.ServerCertificate = selfSignedCertificate;
        });

        var services = CreateServices((gs, environment, config) => { }, services =>
        {
            services.Configure<X509ChainOptions>(options =>
            {
                options.AdditionalCustomTrustCertificates = [selfSignedCertificate, CreateSelfSignedCert("example.com")];
            });
        });

        var httpClient = services.GetRequiredService<IHttpClientFactory>().CreateClient();

        var response = await httpClient.GetStringAsync("https://localhost:55557");
        Assert.Equal("Hi", response);
    }

    [Fact]
    public async Task CallHttp_ReachingOutToServerTrustedThroughSystemCA()
    {
        var services = CreateServices((gs, environment, config) => { }, services =>
        {
            services.Configure<X509ChainOptions>(options =>
            {
                options.AdditionalCustomTrustCertificates = [];
            });
        });

        var httpClient = services.GetRequiredService<IHttpClientFactory>().CreateClient();

        var response = await httpClient.GetAsync("https://example.com");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CallHttpWithCustomTrustForSelfSigned_ReachingOutToServerTrustedThroughSystemCA()
    {
        var selfSignedCertificate = CreateSelfSignedCert("localhost");
        var services = CreateServices((gs, environment, config) => { }, services =>
        {
            services.Configure<X509ChainOptions>(options =>
            {
                options.AdditionalCustomTrustCertificates = [selfSignedCertificate];
            });
        });

        var httpClient = services.GetRequiredService<IHttpClientFactory>().CreateClient();

        var response = await httpClient.GetAsync("https://example.com");
        response.EnsureSuccessStatusCode();
    }

    private static async Task<IAsyncDisposable> CreateServerAsync(int port, Action<HttpsConnectionAdapterOptions> configure)
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.Services.AddRoutingCore();
        builder.WebHost.UseKestrelCore()
            .ConfigureKestrel(options =>
            {
                options.ListenLocalhost(port, listenOptions =>
                {
                    listenOptions.UseHttps(httpsOptions =>
                    {
                        configure(httpsOptions);
                    });
                });
            });

        var app = builder.Build();

        app.MapGet("/", () => "Hi");

        await app.StartAsync();

        return app;
    }

    private static X509ChainOptions CreateOptions(Action<GlobalSettings, IHostEnvironment, Dictionary<string, string>> configure, Action<IServiceCollection>? after = null)
    {
        var services = CreateServices(configure, after);
        return services.GetRequiredService<IOptions<X509ChainOptions>>().Value;
    }

    private static IServiceProvider CreateServices(Action<GlobalSettings, IHostEnvironment, Dictionary<string, string>> configure, Action<IServiceCollection>? after = null)
    {
        var globalSettings = new GlobalSettings
        {
            // A solid default for these tests as these settings aren't allowed to work in cloud.
            SelfHosted = true,
        };
        var hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.EnvironmentName = "Development";
        var config = new Dictionary<string, string>();

        configure(globalSettings, hostEnvironment, config);

        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFakeLogging();
        });
        services.AddSingleton(globalSettings);
        services.AddSingleton(hostEnvironment);
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(config)
                .Build()
        );

        services.AddX509ChainCustomization();

        after?.Invoke(services);

        return services.BuildServiceProvider();
    }
}
