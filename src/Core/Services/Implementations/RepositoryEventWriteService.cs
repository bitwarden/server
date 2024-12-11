using Bit.Core.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public class RepositoryEventWriteService : IEventWriteService
{
    private readonly IEventRepository _eventRepository;

    public RepositoryEventWriteService(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public async Task CreateAsync(IEvent e)
    {
        await _eventRepository.CreateAsync(e);
    }

    public async Task CreateManyAsync(IEnumerable<IEvent> e)
    {
        await _eventRepository.CreateManyAsync(e);
    }
}
