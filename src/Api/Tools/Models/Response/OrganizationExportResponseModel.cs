using Bit.Api.Models.Response;
using Bit.Api.Vault.Models.Response;
using Bit.Core.Entities;
using Bit.Core.Models.Api;
using Bit.Core.Settings;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Tools.Models.Response;

public class OrganizationExportResponseModel : ResponseModel
{
    public OrganizationExportResponseModel()
        : base("organizationExport") { }

    public OrganizationExportResponseModel(
        IEnumerable<CipherOrganizationDetailsWithCollections> ciphers,
        IEnumerable<Collection> collections,
        GlobalSettings globalSettings
    )
        : this()
    {
        Ciphers = ciphers.Select(c => new CipherMiniDetailsResponseModel(c, globalSettings));
        Collections = collections.Select(c => new CollectionResponseModel(c));
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
