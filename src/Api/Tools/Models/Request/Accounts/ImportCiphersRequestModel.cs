// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.Vault.Models.Request;

namespace Bit.Api.Tools.Models.Request.Accounts;

public class ImportCiphersRequestModel
{
    public FolderWithIdRequestModel[] Folders { get; set; }
    public CipherRequestModel[] Ciphers { get; set; }
    public KeyValuePair<int, int>[] FolderRelationships { get; set; }
}
