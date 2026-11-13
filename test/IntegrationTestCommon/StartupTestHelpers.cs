using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Bit.IntegrationTestCommon;

public static class StartupTestHelpers
{
    /// <summary>
    /// Starts the host of <paramref name="factory"/> in <paramref name="environment"/>, and asserts that startup
    /// fails with a message that contains <paramref name="expectedMessageFragment"/>.
    /// </summary>
    public static Exception AssertStartupFails<TEntryPoint>(WebApplicationFactory<TEntryPoint> factory,
        string environment, string expectedMessageFragment)
        where TEntryPoint : class
    {
        using var environmentFactory = factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));

        var exception = Assert.ThrowsAny<Exception>(() => environmentFactory.CreateClient());

        Assert.Contains(expectedMessageFragment, exception.Message);
        return exception;
    }

    /// <summary>
    /// Gets the startup error message for a certificate at <paramref name="settingPath"/> that does not have an RSA
    /// key.
    /// </summary>
    public static string NoRsaKeyMessage(string settingPath, string thumbprint) =>
        $"The certificate for {settingPath} with thumbprint {thumbprint} does not have an RSA key. " +
        "Each Identity signing, SSO signing, and SSO encryption certificate must have an RSA key. " +
        "The host signs tokens with RS256, signs SAML messages with RSA-SHA, " +
        "and decrypts SAML assertions with RSA-OAEP.";

    /// <summary>
    /// Gets the certificate source entries that the host wrote before its "{Project} started." entry. Each entry
    /// has the <see cref="Bit.Core.Constants.BypassFiltersEventId"/> event ID and a "Role" value.
    /// </summary>
    public static IReadOnlyList<CollectedLogEntry> GetCertificateSourceEntries(CollectingLoggerProvider logs)
    {
        var entries = logs.Entries;
        var started = entries.ToList().FindIndex(entry =>
            entry.Values.GetValueOrDefault("{OriginalFormat}") == "{Project} started.");
        Assert.True(started >= 0, "The host did not log \"{Project} started.\".");
        return entries
            .Take(started)
            .Where(entry => entry.Level == LogLevel.Information &&
                            entry.EventId.Id == Bit.Core.Constants.BypassFiltersEventId &&
                            entry.Values.ContainsKey("Role"))
            .ToArray();
    }
}
