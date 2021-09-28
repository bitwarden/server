using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api
{
    public class SsoConfigResponseModel
    {
        public SsoConfigResponseModel() { }

        public SsoConfigResponseModel(SsoConfigurationData ssoConfigData)
        {
            if (ssoConfigData == null)
            {
                return;
            }

            CryptoAgentUrl = ssoConfigData.UseCryptoAgent ? ssoConfigData.CryptoAgentUrl : null;
        }

        public string CryptoAgentUrl { get; set; }
    }
}
