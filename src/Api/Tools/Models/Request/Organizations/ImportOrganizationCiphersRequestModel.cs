// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.Models.Request;
using Bit.Api.Vault.Models.Request;

namespace Bit.Api.Tools.Models.Request.Organizations;

public class ImportOrganizationCiphersRequestModel
{
    public CollectionWithIdRequestModel[] Collections { get; set; }
    public CipherRequestModel[] Ciphers { get; set; }
    public KeyValuePair<int, int>[] CollectionRelationships { get; set; }
}
