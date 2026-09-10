// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.AdminConsole.Models.Response;
using Bit.Api.Models.Response;
using Bit.Api.Vault.Models.Response;
using Bit.Core.Entities;
using Bit.Core.Models.Api;
using Bit.Core.Settings;
using Bit.Core.Vault.Authorization;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Tools.Models.Response;

public class OrganizationExportResponseModel : ResponseModel
{
    public OrganizationExportResponseModel() : base("organizationExport")
    {
    }

    /// <remarks>
    /// Expects ciphers already reduced to those the witness authorizes. An export carries full data or
    /// nothing, so a cipher the witness does not cover throws here rather than being quietly reshaped or
    /// dropped.
    /// </remarks>
    public OrganizationExportResponseModel(IEnumerable<CipherOrganizationDetailsWithCollections> ciphers,
        IEnumerable<Collection> collections, GlobalSettings globalSettings, FullCipherAccess fullCipherAccess) : this()
    {
        Ciphers = ciphers.Select(c => new FullCipherMiniDetailsResponseModel(fullCipherAccess, c, globalSettings));
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
