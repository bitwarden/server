using System.Data;
using Bit.Core.Entities;

namespace Bit.Core.Models.Data;

public class GroupWithCollections : Group
{
    public DataTable Collections { get; set; }
}
