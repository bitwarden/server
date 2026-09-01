using Bit.Pam.Models;

namespace Bit.Services.Pam.AccessConnector.Models;

/// <summary>
/// A rotation daemon's detail view: its <see cref="PamAccessConnectorListItem"/> projection together with the recent jobs it
/// has worked (newest first, each carrying the attempts that daemon recorded) — the read model for
/// <c>GET daemons/{id}</c>.
/// </summary>
public sealed record PamAccessConnectorHistory(
    PamAccessConnectorListItem Daemon,
    IReadOnlyList<PamRotationJobDetails> Jobs);
