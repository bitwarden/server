using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response;

public class OrganizationExportResponseModel : ResponseModel
{
    public OrganizationExportResponseModel() : base("organizationExport")
    {
    }

    public IEnumerable<CollectionResponseModel> Collections { get; set; }

    public IEnumerable<CipherMiniDetailsResponseModel> Ciphers { get; set; }
}

[Obsolete("This version is for backwards compatibility for client version 2022.9.0")]
public class OrganizationExportListResponseModel
{
    public ListResponseModel<CollectionResponseModel> Collections { get; set; }

    public ListResponseModel<CipherMiniDetailsResponseModel> Ciphers { get; set; }
}
