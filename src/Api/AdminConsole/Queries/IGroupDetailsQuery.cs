namespace Api.AdminConsole.Queries;

public interface IGroupDetailsQuery
{
    Task<IEnumerable<GroupDetailsQueryResponse>> GetGroupDetails(GroupDetailsQueryRequest request);
}
