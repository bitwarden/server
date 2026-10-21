using Bit.Pam.Entities;

namespace Bit.Services.Pam.AccessConnector.Models;

/// <summary>
/// The result of registering an access connector. <see cref="ClientSecret"/> is the plaintext secret, returned only
/// here and never persisted or logged.
/// </summary>
public sealed record PamAccessConnectorRegistrationResult(PamAccessConnector AccessConnector, string ClientSecret);
