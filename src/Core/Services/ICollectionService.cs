using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System;

namespace Bit.Core.Services
{
    public interface ICollectionService
    {
        Task SaveAsync(Collection collection, IEnumerable<Guid> groupIds = null);
    }
}
