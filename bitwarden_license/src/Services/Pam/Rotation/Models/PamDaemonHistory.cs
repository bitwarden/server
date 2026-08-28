using Bit.Pam.Models;

namespace Bit.Services.Pam.Rotation.Models;

/// <summary>
/// A rotation daemon's detail view: its <see cref="PamDaemonListItem"/> projection together with the recent jobs it
/// has worked (newest first, each carrying the attempts that daemon recorded) — the read model for
/// <c>GET daemons/{id}</c>.
/// </summary>
public sealed record PamDaemonHistory(
    PamDaemonListItem Daemon,
    IReadOnlyList<PamRotationJobDetails> Jobs);
