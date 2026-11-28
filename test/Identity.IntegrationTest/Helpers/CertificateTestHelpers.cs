using Bit.IntegrationTestCommon.Factories;

namespace Bit.Identity.IntegrationTest.Helpers;

/// <summary>
/// Arrange helpers for tests that run the Identity host with a given certificate configuration.
/// </summary>
public static class CertificateTestHelpers
{
    public static IdentityApplicationFactory CreateFactory(Dictionary<string, string?> configuration)
    {
        var factory = new IdentityApplicationFactory();
        foreach (var (key, value) in configuration)
        {
            factory.UpdateConfiguration(key, value);
        }
        return factory;
    }
}
