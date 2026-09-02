// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Data;
using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.Models.Data;

public class GroupWithCollections : Group
{
    public DataTable Collections { get; set; }
}
