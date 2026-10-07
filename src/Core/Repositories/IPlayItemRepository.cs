using Bit.Core.Entities;

#nullable enable

namespace Bit.Core.Repositories;

public interface IPlayItemRepository : IRepository<PlayItem, Guid>
{
    Task<ICollection<PlayItem>> GetByPlayIdAsync(string playId);
    Task DeleteByPlayIdAsync(string playId);
}
