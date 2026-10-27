using Bit.Pam;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Models;
using Bit.Services.Pam.AccessConnector.Queries.Interfaces;
using Microsoft.Extensions.Options;

namespace Bit.Services.Pam.AccessConnector.Queries;

/// <inheritdoc cref="IListAccessConnectorsQuery" />
public class ListAccessConnectorsQuery : IListAccessConnectorsQuery
{
    private readonly IPamAccessConnectorRepository _accessConnectorRepository;
    private readonly IOptions<PamRotationOptions> _options;
    private readonly TimeProvider _timeProvider;

    public ListAccessConnectorsQuery(
        IPamAccessConnectorRepository accessConnectorRepository,
        IOptions<PamRotationOptions> options,
        TimeProvider timeProvider)
    {
        _accessConnectorRepository = accessConnectorRepository;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<ICollection<PamAccessConnectorListItem>> ListAsync(Guid organizationId)
    {
        var accessConnectors = await _accessConnectorRepository.GetManyByOrganizationIdAsync(organizationId);
        var assignments = await _accessConnectorRepository.GetAssignmentsByOrganizationIdAsync(organizationId);
        var assignmentsByAccessConnector = assignments
            .GroupBy(a => a.AccessConnectorId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(a => a.TargetSystemId).ToList());

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var offlineAfter = _options.Value.AccessConnectorOfflineAfter;

        return accessConnectors
            .Select(accessConnector => new PamAccessConnectorListItem(
                accessConnector,
                PamRotationRules.IsConnected(accessConnector, now, offlineAfter),
                assignmentsByAccessConnector.TryGetValue(accessConnector.Id, out var targetIds) ? targetIds : []))
            .ToList();
    }
}
