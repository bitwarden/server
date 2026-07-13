using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Repositories;

namespace Bit.Core.Dirt.Services.Implementations;

/// <summary>
/// Purges an organization's event logs (e.g. from Table Storage) after the organization is deleted,
/// satisfying the <see cref="OrganizationDeleteTaskType.EventsCleanup"/> task type.
/// </summary>
public class EventsCleanupOrganizationDeleteTaskHandler : IOrganizationDeleteTaskHandler
{
    private readonly IEventRepository _eventRepository;

    public EventsCleanupOrganizationDeleteTaskHandler(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public OrganizationDeleteTaskType TaskType => OrganizationDeleteTaskType.EventsCleanup;

    public Task<int> DeleteBatchAsync(OrganizationDeleteTask task, CancellationToken cancellationToken)
        => _eventRepository.DeleteManyByOrganizationIdAsync(task.OrganizationId, cancellationToken);
}
