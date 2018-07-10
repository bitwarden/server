using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using Bit.Core.Models.Data;
using System;

namespace Bit.Core.Services
{
    public interface IGroupService
    {
        Task SaveAsync(Group group, IEnumerable<SelectionReadOnly> collections = null);
        Task DeleteAsync(Group group);
        Task DeleteUserAsync(Group group, Guid organizationUserId);
    }
}
