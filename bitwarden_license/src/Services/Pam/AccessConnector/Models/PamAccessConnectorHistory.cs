using Bit.Pam.Models;

namespace Bit.Services.Pam.AccessConnector.Models;

/// <summary>
/// An access connector's detail view: its list projection plus its recent jobs, newest first, each with only the
/// attempts it recorded.
/// </summary>
public sealed record PamAccessConnectorHistory(
    PamAccessConnectorListItem AccessConnector,
    IReadOnlyList<PamRotationJobDetails> Jobs);
