using Bit.Core.Repositories;
using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Repositories;

public interface ISendRepository : IRepository<Send, Guid>
{
    Task<ICollection<Send>> GetManyByUserIdAsync(Guid userId);
    Task<ICollection<Send>> GetManyByDeletionDateAsync(DateTime deletionDateBefore);
}
