using System.Diagnostics.Metrics;

namespace Bit.Sso.Utilities;

/// <summary>
/// Emits anonymous, aggregate metrics about SAML assertion inspection.
/// This class never records an organization identifier, a user identifier, or any other identifying value.
/// </summary>
public class Saml2AssertionMetrics
{
    private readonly Counter<long> _unsupportedKeyTransportAlgorithmCounter;

    public Saml2AssertionMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Bitwarden.Sso.Saml2");
        _unsupportedKeyTransportAlgorithmCounter = meter.CreateCounter<long>(
            "bitwarden.sso.saml2.unsupported_key_transport_algorithm",
            unit: "{requests}",
            description: "Number of SAML ACS requests carrying at least one assertion key that uses an unsupported key transport algorithm.");
    }

    /// <summary>
    /// Records one occurrence of an unsupported key transport algorithm.
    /// </summary>
    /// <param name="algorithm">
    /// The unaccepted algorithm URI, or <see langword="null"/> when the assertion names no algorithm.
    /// The set of possible values is small and fixed. It never carries an organization or a user identifier.
    /// </param>
    public void RecordUnsupportedKeyTransportAlgorithm(string? algorithm)
    {
        _unsupportedKeyTransportAlgorithmCounter.Add(1,
            new KeyValuePair<string, object?>("algorithm", algorithm ?? "none"));
    }
}
