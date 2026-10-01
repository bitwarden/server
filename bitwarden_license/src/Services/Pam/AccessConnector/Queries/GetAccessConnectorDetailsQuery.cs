using Bit.Core.Exceptions;
using Bit.Pam;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Models;
using Bit.Services.Pam.AccessConnector.Queries.Interfaces;
using Microsoft.Extensions.Options;

namespace Bit.Services.Pam.AccessConnector.Queries;

/// <inheritdoc cref="IGetAccessConnectorDetailsQuery" />
public class GetAccessConnectorDetailsQuery : IGetAccessConnectorDetailsQuery
{
    /// <summary>
    /// How many of the daemon's jobs the detail page shows. A daemon accumulates a job per rotation it executes for
    /// the lifetime of the fleet, so this read is capped rather than unbounded.
    /// </summary>
    private const int RecentJobLimit = 50;

    private readonly IPamDaemonRepository _daemonRepository;
    private readonly IPamRotationJobRepository _jobRepository;
    private readonly IOptions<PamRotationOptions> _options;
    private readonly TimeProvider _timeProvider;

    public GetAccessConnectorDetailsQuery(
        IPamDaemonRepository daemonRepository,
        IPamRotationJobRepository jobRepository,
        IOptions<PamRotationOptions> options,
        TimeProvider timeProvider)
    {
        _daemonRepository = daemonRepository;
        _jobRepository = jobRepository;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<PamAccessConnectorHistory> GetAsync(Guid organizationId, Guid daemonId)
    {
        var daemon = await _daemonRepository.GetByIdAsync(daemonId);
        if (daemon is null || daemon.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var assignments = await _daemonRepository.GetAssignmentsByOrganizationIdAsync(organizationId);
        var jobs = await _jobRepository.GetManyRecentByDaemonIdAsync(daemonId, RecentJobLimit);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var listItem = new PamAccessConnectorListItem(
            daemon,
            PamRotationRules.IsConnected(daemon, now, _options.Value.DaemonOfflineAfter),
            assignments.Where(a => a.DaemonId == daemonId).Select(a => a.TargetSystemId).ToList());

        return new PamAccessConnectorHistory(listItem, jobs.ToList());
    }
}
