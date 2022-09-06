using Bit.Scim.Models;

namespace Bit.Scim.Commands.Groups.Interfaces
{
    public interface IGetGroupsListCommand
    {
        Task<ScimListResponseModel<ScimGroupResponseModel>> GetGroupsListAsync(Guid organizationId, string filter, int? count, int? startIndex);
    }
}
