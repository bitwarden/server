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
