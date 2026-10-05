using Bit.Pam.Entities;

namespace Bit.Services.Pam.AccessConnector.Models;

/// <summary>
/// The result of registering an access connector. <see cref="ClientSecret"/> is the plaintext client secret for the
/// access connector's <c>dbo.ApiKey</c> credential, surfaced to the caller here only; the server never persists or logs
/// it.
/// </summary>
public sealed record PamAccessConnectorRegistrationResult(PamAccessConnector AccessConnector, string ClientSecret);
