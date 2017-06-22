using System;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class TwoFactorDuoResponseModel : ResponseModel
    {
        public TwoFactorDuoResponseModel(User user)
            : base("twoFactorDuo")
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Duo);
            if(provider?.MetaData != null && provider.MetaData.Count > 0)
            {
                Enabled = provider.Enabled;

                if(provider.MetaData.ContainsKey("Host"))
                {
                    Host = (string)provider.MetaData["Host"];
                }
                if(provider.MetaData.ContainsKey("SKey"))
                {
                    SecretKey = (string)provider.MetaData["SKey"];
                }
                if(provider.MetaData.ContainsKey("IKey"))
                {
                    IntegrationKey = (string)provider.MetaData["IKey"];
                }
            }
            else
            {
                Enabled = false;
            }
        }

        public bool Enabled { get; set; }
        public string Host { get; set; }
        public string SecretKey { get; set; }
        public string IntegrationKey { get; set; }
    }
}
