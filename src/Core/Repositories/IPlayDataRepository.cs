using Bit.Core.Entities;

#nullable enable

namespace Bit.Core.Repositories;

public interface IPlayDataRepository : IRepository<PlayData, Guid>
{
    Task<ICollection<PlayData>> GetByPlayIdAsync(string playId);
    Task DeleteByPlayIdAsync(string playId);
}
