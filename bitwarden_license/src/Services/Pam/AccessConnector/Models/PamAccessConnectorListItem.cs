using Bit.Pam.Entities;

namespace Bit.Services.Pam.AccessConnector.Models;

/// <summary>
/// An access connector together with its derived liveness (spec <c>ConnectorConnection</c>, see
/// <c>PamRotationRules.IsConnected</c>) and the target systems it is assigned to — the list view model for the
/// access connectors admin surface.
/// </summary>
public sealed record PamAccessConnectorListItem(
    PamAccessConnector AccessConnector,
    bool IsConnected,
    IReadOnlyList<Guid> AssignedTargetSystemIds);
