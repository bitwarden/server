using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.SeederApi.Queries.Interfaces;

namespace Bit.SeederApi.Queries;

public class GetAllPlayIdsQuery(DatabaseContext databaseContext) : IGetAllPlayIdsQuery
{
    public List<string> GetAllPlayIds()
    {
        return databaseContext.PlayItem
            .Select(pd => pd.PlayId)
            .Distinct()
            .ToList();
    }

    public List<string> GetAllPlayIds(DateTime olderThan)
    {
        return databaseContext.PlayItem
            .Where(pd => pd.CreationDate < olderThan)
            .Select(pd => pd.PlayId)
            .Distinct()
            .ToList();
    }
}
