using System.Data;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Data
{
    public class OrganizationUserWithCollections : OrganizationUser
    {
        public DataTable Collections { get; set; }
    }
}
