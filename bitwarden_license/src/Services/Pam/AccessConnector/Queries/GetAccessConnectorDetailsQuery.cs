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
    /// How many of the access connector's jobs the detail page shows. An access connector accumulates a job per
    /// rotation it executes for the lifetime of the fleet, so this read is capped rather than unbounded.
    /// </summary>
    private const int RecentJobLimit = 50;

    private readonly IPamAccessConnectorRepository _accessConnectorRepository;
    private readonly IPamRotationJobRepository _jobRepository;
    private readonly IOptions<PamRotationOptions> _options;
    private readonly TimeProvider _timeProvider;

    public GetAccessConnectorDetailsQuery(
        IPamAccessConnectorRepository accessConnectorRepository,
        IPamRotationJobRepository jobRepository,
        IOptions<PamRotationOptions> options,
        TimeProvider timeProvider)
    {
        _accessConnectorRepository = accessConnectorRepository;
        _jobRepository = jobRepository;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<PamAccessConnectorHistory> GetAsync(Guid organizationId, Guid accessConnectorId)
    {
        var accessConnector = await _accessConnectorRepository.GetByIdAsync(accessConnectorId);
        if (accessConnector is null || accessConnector.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var assignments = await _accessConnectorRepository.GetAssignmentsByOrganizationIdAsync(organizationId);
        var jobs = await _jobRepository.GetManyRecentByAccessConnectorIdAsync(accessConnectorId, RecentJobLimit);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var listItem = new PamAccessConnectorListItem(
            accessConnector,
            PamRotationRules.IsConnected(accessConnector, now, _options.Value.AccessConnectorOfflineAfter),
            assignments.Where(a => a.AccessConnectorId == accessConnectorId).Select(a => a.TargetSystemId).ToList());

        return new PamAccessConnectorHistory(listItem, jobs.ToList());
    }
}
