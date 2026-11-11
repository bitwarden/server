using Bit.Pam.Entities;

namespace Bit.Services.Pam.AccessConnector.Models;

/// <summary>
/// An access connector with its derived liveness (spec <c>ConnectorConnection</c>) and assigned target systems.
/// </summary>
public sealed record PamAccessConnectorListItem(
    PamAccessConnector AccessConnector,
    bool IsConnected,
    IReadOnlyList<Guid> AssignedTargetSystemIds);
