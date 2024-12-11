using Bit.Core.AdminConsole.OrganizationAuth.Models;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request;

public class OrganizationAuthRequestUpdateManyRequestModel
{
    public Guid Id { get; set; }

    [EncryptedString]
    public string Key { get; set; }

    public bool Approved { get; set; }

    public OrganizationAuthRequestUpdate ToOrganizationAuthRequestUpdate()
    {
        return new OrganizationAuthRequestUpdate
        {
            Id = Id,
            Key = Key,
            Approved = Approved,
        };
    }
}
