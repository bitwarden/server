using System.Threading.Tasks;
using Bit.Core.Repositories;
using System.Collections.Generic;
using Bit.Core.Models.Data;

namespace Bit.Core.Services
{
    public class RepositoryEventWriteService : IEventWriteService
    {
        private readonly IEventRepository _eventRepository;

        public RepositoryEventWriteService(
            IEventRepository eventRepository)
        {
            _eventRepository = eventRepository;
        }

        public async Task CreateAsync(IEvent e)
        {
            await _eventRepository.CreateAsync(e);
        }

        public async Task CreateManyAsync(IList<IEvent> e)
        {
            await _eventRepository.CreateManyAsync(e);
        }
    }
}
