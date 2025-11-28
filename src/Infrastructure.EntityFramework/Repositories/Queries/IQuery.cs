namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public interface IQuery<TOut>
{
    IQueryable<TOut> Run(DatabaseContext dbContext);
}
