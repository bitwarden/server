using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System;

namespace Bit.Core.Services
{
    public interface IGroupService
    {
        Task SaveAsync(Group group, IEnumerable<Guid> collectionIds = null);
    }
}
