using System;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class TwoFactorAuthenticatorResponseModel : ResponseModel
    {
        public TwoFactorAuthenticatorResponseModel(User user)
            : base("twoFactorAuthenticator")
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Authenticator);
            if(provider?.MetaData?.ContainsKey("Key") ?? false)
            {
                Key = provider.MetaData["Key"];
                Enabled = provider.Enabled;
            }
            else
            {
                Enabled = false;
            }
        }

        public bool Enabled { get; set; }
        public string Key { get; set; }
    }
}
