using System.Threading.Tasks;
using Bit.Core.Repositories;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage.Table;

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

        public async Task CreateAsync(ITableEntity entity)
        {
            await _eventRepository.CreateAsync(entity);
        }

        public async Task CreateManyAsync(IList<ITableEntity> entities)
        {
            await _eventRepository.CreateManyAsync(entities);
        }
    }
}
