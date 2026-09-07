using Bit.Pam.Entities;

namespace Bit.Services.Pam.AccessConnector.Models;

/// <summary>
/// The result of registering a rotation daemon. <see cref="ClientSecret"/> is the plaintext client secret for the
/// daemon's <c>dbo.ApiKey</c> credential, surfaced to the caller here only; the server never persists or logs it.
/// </summary>
public sealed record PamAccessConnectorRegistrationResult(PamDaemon Daemon, string ClientSecret);
