using Bit.Core.Entities;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;
using Bit.Core.Settings;

namespace Bit.Api.Models.Response.Organizations;

public class OrganizationSsoResponseModel : ResponseModel
{
    public OrganizationSsoResponseModel(Organization organization, GlobalSettings globalSettings,
        SsoConfig config = null) : base("organizationSso")
    {
        if (config != null)
        {
            Enabled = config.Enabled;
            Data = config.GetData();
        }

        Identifier = organization.Identifier;
        Urls = new SsoUrls(organization.Id.ToString(), globalSettings);
    }

    public bool Enabled { get; set; }
    public string Identifier { get; set; }
    public SsoConfigurationData Data { get; set; }
    public SsoUrls Urls { get; set; }
}

public class SsoUrls
{
    public SsoUrls(string organizationId, GlobalSettings globalSettings)
    {
        CallbackPath = SsoConfigurationData.BuildCallbackPath(globalSettings.BaseServiceUri.Sso);
        SignedOutCallbackPath = SsoConfigurationData.BuildSignedOutCallbackPath(globalSettings.BaseServiceUri.Sso);
        SpEntityId = SsoConfigurationData.BuildSaml2ModulePath(globalSettings.BaseServiceUri.Sso);
        SpMetadataUrl = SsoConfigurationData.BuildSaml2MetadataUrl(globalSettings.BaseServiceUri.Sso, organizationId);
        SpAcsUrl = SsoConfigurationData.BuildSaml2AcsUrl(globalSettings.BaseServiceUri.Sso, organizationId);
    }

    public string CallbackPath { get; set; }
    public string SignedOutCallbackPath { get; set; }
    public string SpEntityId { get; set; }
    public string SpMetadataUrl { get; set; }
    public string SpAcsUrl { get; set; }
}
