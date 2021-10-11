using System.Text.Json;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Settings;

namespace Bit.Core.Models.Api
{
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
            else
            {
                Data = new SsoConfigurationData();
            }

            Urls = new SsoUrls(organization.Id.ToString(), Data, globalSettings);
        }

        public bool Enabled { get; set; }
        public SsoConfigurationData Data { get; set; }
        public SsoUrls Urls { get; set; }
    }

    public class SsoUrls
    {
        public SsoUrls(string organizationId, SsoConfigurationData configurationData, GlobalSettings globalSettings)
        {
            CallbackPath = configurationData.BuildCallbackPath(globalSettings.BaseServiceUri.Sso);
            SignedOutCallbackPath = configurationData.BuildSignedOutCallbackPath(globalSettings.BaseServiceUri.Sso);
            SpEntityId = configurationData.BuildSaml2ModulePath(globalSettings.BaseServiceUri.Sso);
            SpMetadataUrl = configurationData.BuildSaml2MetadataUrl(globalSettings.BaseServiceUri.Sso, organizationId);
            SpAcsUrl = configurationData.BuildSaml2AcsUrl(globalSettings.BaseServiceUri.Sso, organizationId);
        }

        public string CallbackPath { get; set; }
        public string SignedOutCallbackPath { get; set; }
        public string SpEntityId { get; set; }
        public string SpMetadataUrl { get; set; }
        public string SpAcsUrl { get; set; }
    }
}
