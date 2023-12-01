using System.Data;
using Bit.Core.Entities;

namespace Bit.Core.Models.Data.Organizations.OrganizationUsers;

public class OrganizationUserWithCollections : OrganizationUser
{
    public DataTable Collections { get; set; }
}
