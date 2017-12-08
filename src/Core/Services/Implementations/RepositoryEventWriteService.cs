using System.Threading.Tasks;
using Bit.Core.Repositories;
using System.Collections.Generic;
using Bit.Core.Models.Data;

namespace Bit.Core.Services
{
    public class RepositoryEventWriteService : IEventWriteService
    {
        private readonly IEventRepository _eventRepository;
        private readonly GlobalSettings _globalSettings;

        public RepositoryEventWriteService(
            IEventRepository eventRepository,
            GlobalSettings globalSettings)
        {
            _eventRepository = eventRepository;
            _globalSettings = globalSettings;
        }

        public async Task CreateAsync(EventTableEntity entity)
        {
            await _eventRepository.CreateAsync(entity);
        }

        public async Task CreateManyAsync(IList<EventTableEntity> entities)
        {
            await _eventRepository.CreateManyAsync(entities);
        }
    }
}
