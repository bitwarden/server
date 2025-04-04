using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Bit.Core.Platform.X509ChainCustomization;
using Bit.Core.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            gs.SelfHosted = true;

            environment.EnvironmentName = "Development";

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

            environment.EnvironmentName = "Development";

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

        var options = CreateOptions((gs, environment, config) =>
        {
            gs.SelfHosted = false;

            environment.EnvironmentName = "Development";

            config["X509ChainOptions:AdditionalCustomTrustCertificatesDirectory"] = tempDir.FullName;
        }, services =>
        {
            services.Configure<X509ChainOptions>(options =>
            {
                options.AdditionalCustomTrustCertificates = [CreateSelfSignedCert("example.com")];
            });
        });

        Assert.True(options.TryGetCustomRemoteCertificateValidationCallback(out var callback));
        var cert = Assert.Single(options.AdditionalCustomTrustCertificates);
        Assert.Equal("CN=example.com", cert.Subject);
    }

    private static X509ChainOptions CreateOptions(Action<GlobalSettings, IHostEnvironment, Dictionary<string, string>> configure, Action<IServiceCollection>? after = null)
    {
        var services = CreateServices(configure, after);
        return services.GetRequiredService<IOptions<X509ChainOptions>>().Value;
    }

    private static IServiceProvider CreateServices(Action<GlobalSettings, IHostEnvironment, Dictionary<string, string>> configure, Action<IServiceCollection>? after = null)
    {
        var globalSettings = new GlobalSettings();
        var hostEnvironment = Substitute.For<IHostEnvironment>();
        var config = new Dictionary<string, string>();

        configure(globalSettings, hostEnvironment, config);

        var services = new ServiceCollection();
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
