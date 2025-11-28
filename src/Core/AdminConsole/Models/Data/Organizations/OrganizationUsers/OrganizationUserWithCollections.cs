// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Data;
using Bit.Core.Entities;

namespace Bit.Core.Models.Data.Organizations.OrganizationUsers;

public class OrganizationUserWithCollections : OrganizationUser
{
    public DataTable Collections { get; set; }
}
