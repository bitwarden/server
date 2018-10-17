using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using Bit.Core.Models.Data;
using System;

namespace Bit.Core.Services
{
    public interface ICollectionService
    {
        Task SaveAsync(Collection collection, IEnumerable<SelectionReadOnly> groups = null, Guid? assignUserId = null);
        Task DeleteAsync(Collection collection);
        Task DeleteUserAsync(Collection collection, Guid organizationUserId);
    }
}
