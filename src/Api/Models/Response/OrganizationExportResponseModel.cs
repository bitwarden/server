namespace Bit.Api.Models.Response;

public class OrganizationExportResponseModel
{
    public OrganizationExportResponseModel()
    {
    }

    public ListResponseModel<CollectionResponseModel> Collections { get; set; }

    public ListResponseModel<CipherMiniDetailsResponseModel> Ciphers { get; set; }
}
