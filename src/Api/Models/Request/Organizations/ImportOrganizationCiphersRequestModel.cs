namespace Bit.Api.Models.Request.Organizations;

public class ImportOrganizationCiphersRequestModel
{
    public CollectionWithIdRequestModel[] Collections { get; set; }
    public CipherRequestModel[] Ciphers { get; set; }
    public KeyValuePair<int, int>[] CollectionRelationships { get; set; }
}
