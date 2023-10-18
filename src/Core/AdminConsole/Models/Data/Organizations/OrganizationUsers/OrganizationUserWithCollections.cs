using System.Data;
using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.OrganizationUsers;

public class OrganizationUserWithCollections : OrganizationUser
{
    public DataTable Collections { get; set; }
}
