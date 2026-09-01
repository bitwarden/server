using Bit.Pam.Entities;

namespace Bit.Services.Pam.AccessConnector.Models;

/// <summary>
/// The result of registering a rotation daemon (spec <c>DaemonRegistration</c>). <see cref="ClientSecret"/> is the
/// plaintext client secret for the daemon's <c>dbo.ApiKey</c> credential — it is generated once here, hashed for
/// storage, and surfaced to the caller exactly once; the server never persists or logs it.
/// </summary>
public sealed record PamAccessConnectorRegistrationResult(PamDaemon Daemon, string ClientSecret);
