using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System;
using Bit.Core.Models.Data;

namespace Bit.Core.Services
{
    public interface ICollectionService
    {
        Task SaveAsync(Collection collection, IEnumerable<SelectionReadOnly> groups = null);
    }
}
