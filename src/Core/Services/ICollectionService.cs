using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface ICollectionService
    {
        Task SaveAsync(Collection collection, IEnumerable<SelectionReadOnly> groups = null, Guid? assignUserId = null);
        Task DeleteAsync(Collection collection);
        Task DeleteUserAsync(Collection collection, Guid organizationUserId);
    }
}
