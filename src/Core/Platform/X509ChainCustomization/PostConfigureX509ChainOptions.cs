#nullable enable

using System.Security.Cryptography.X509Certificates;
using Bit.Core.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bit.Core.Platform.X509ChainCustomization;

internal sealed class PostConfigureX509ChainOptions : IPostConfigureOptions<X509ChainOptions>
{
    const string CertificateSearchPattern = "*.crt";

    private readonly ILogger<PostConfigureX509ChainOptions> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly GlobalSettings _globalSettings;

    public PostConfigureX509ChainOptions(
        ILogger<PostConfigureX509ChainOptions> logger,
        IHostEnvironment hostEnvironment,
        GlobalSettings globalSettings)
    {
        _logger = logger;
        _hostEnvironment = hostEnvironment;
        _globalSettings = globalSettings;
    }

    public void PostConfigure(string? name, X509ChainOptions options)
    {
        // We don't register or request a named instance of these options,
        // so don't customize it.
        if (name != Options.DefaultName)
        {
            return;
        }

        // We only allow this setting to be configured on self host.
        if (!_globalSettings.SelfHosted)
        {
            options.AdditionalCustomTrustCertificatesDirectory = null;
            return;
        }

        if (options.AdditionalCustomTrustCertificates != null)
        {
            // Additional certificates were added directly, this overwrites the need to
            // read them from the directory.
            _logger.LogInformation(
                "Additional custom trust certificates were added directly, skipping loading them from '{Directory}'",
                options.AdditionalCustomTrustCertificatesDirectory
            );
            return;
        }

        if (string.IsNullOrEmpty(options.AdditionalCustomTrustCertificatesDirectory))
        {
            return;
        }

        if (!Directory.Exists(options.AdditionalCustomTrustCertificatesDirectory))
        {
            // The default directory is volume mounted via the default Bitwarden setup process.
            // If the directory doesn't exist it could indicate a error in configuration but this
            // directory is never expected in a normal development environment so lower the log
            // level in that case.
            var logLevel = _hostEnvironment.IsDevelopment()
                ? LogLevel.Debug
                : LogLevel.Warning;
            _logger.Log(
                logLevel,
                "An additional custom trust certificate directory was given '{Directory}' but that directory does not exist.",
                options.AdditionalCustomTrustCertificatesDirectory
            );
            return;
        }

        var certificates = new List<X509Certificate2>();

        foreach (var certFile in Directory.EnumerateFiles(options.AdditionalCustomTrustCertificatesDirectory, CertificateSearchPattern))
        {
            certificates.Add(new X509Certificate2(certFile));
        }

        if (options.AdditionalCustomTrustCertificatesDirectory != X509ChainOptions.DefaultAdditionalCustomTrustCertificatesDirectory && certificates.Count == 0)
        {
            // They have intentionally given us a non-default directory but there weren't certificates, that is odd.
            _logger.LogWarning(
                "No additional custom trust certificates were found in '{Directory}'",
                options.AdditionalCustomTrustCertificatesDirectory
            );
        }

        options.AdditionalCustomTrustCertificates = certificates;
    }
}
