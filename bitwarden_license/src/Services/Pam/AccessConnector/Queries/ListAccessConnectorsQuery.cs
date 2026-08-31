using Bit.Pam;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Models;
using Bit.Services.Pam.AccessConnector.Queries.Interfaces;
using Microsoft.Extensions.Options;

namespace Bit.Services.Pam.AccessConnector.Queries;

/// <inheritdoc cref="IListAccessConnectorsQuery" />
public class ListAccessConnectorsQuery : IListAccessConnectorsQuery
{
    private readonly IPamDaemonRepository _daemonRepository;
    private readonly IOptions<PamRotationOptions> _options;
    private readonly TimeProvider _timeProvider;

    public ListAccessConnectorsQuery(
        IPamDaemonRepository daemonRepository, IOptions<PamRotationOptions> options, TimeProvider timeProvider)
    {
        _daemonRepository = daemonRepository;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<ICollection<PamAccessConnectorListItem>> ListAsync(Guid organizationId)
    {
        var daemons = await _daemonRepository.GetManyByOrganizationIdAsync(organizationId);
        var assignments = await _daemonRepository.GetAssignmentsByOrganizationIdAsync(organizationId);
        var assignmentsByDaemon = assignments
            .GroupBy(a => a.DaemonId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(a => a.TargetSystemId).ToList());

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var offlineAfter = _options.Value.DaemonOfflineAfter;

        return daemons
            .Select(daemon => new PamAccessConnectorListItem(
                daemon,
                PamRotationRules.IsConnected(daemon, now, offlineAfter),
                assignmentsByDaemon.TryGetValue(daemon.Id, out var targetIds) ? targetIds : []))
            .ToList();
    }
}
