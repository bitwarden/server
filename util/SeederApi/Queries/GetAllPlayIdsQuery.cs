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
}
