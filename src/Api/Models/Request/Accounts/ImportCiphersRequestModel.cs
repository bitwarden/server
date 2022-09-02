namespace Bit.Api.Models.Request.Accounts;

public class ImportCiphersRequestModel
{
    public FolderRequestModel[] Folders { get; set; }
    public CipherRequestModel[] Ciphers { get; set; }
    public KeyValuePair<int, int>[] FolderRelationships { get; set; }
}
