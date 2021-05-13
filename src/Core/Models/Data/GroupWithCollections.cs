using System.Data;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Data
{
    public class GroupWithCollections : Group
    {
        public DataTable Collections { get; set; }
    }
}
