using System.Linq;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public interface IQuery<TOut>
    {
        IQueryable<TOut> Run(DatabaseContext dbContext);
    }
}
