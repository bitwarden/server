using Bit.Pam.Models;

namespace Bit.Services.Pam.AccessConnector.Models;

/// <summary>
/// An access connector's detail view: its <see cref="PamAccessConnectorListItem"/> projection together with the recent jobs it
/// has worked (newest first, each carrying the attempts that access connector recorded) — the read model for
/// <c>GET access connectors/{id}</c>.
/// </summary>
public sealed record PamAccessConnectorHistory(
    PamAccessConnectorListItem AccessConnector,
    IReadOnlyList<PamRotationJobDetails> Jobs);
