using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationApiKeyInformation : ResponseModel
{
    public OrganizationApiKeyInformation(OrganizationApiKey key)
        : base("keyInformation")
    {
        KeyType = key.Type;
        RevisionDate = key.RevisionDate;
    }

    public OrganizationApiKeyType KeyType { get; set; }
    public DateTime RevisionDate { get; set; }
}
