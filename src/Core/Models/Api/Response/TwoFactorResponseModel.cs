using System;
using Bit.Core.Models.Table;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api
{
    public class TwoFactorResponseModel : ResponseModel
    {
        public TwoFactorResponseModel(User user)
            : base("twoFactor")
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var providers = user.GetTwoFactorProviders();
            if(user.TwoFactorProvider.HasValue && providers.ContainsKey(user.TwoFactorProvider.Value))
            {
                var provider = providers[user.TwoFactorProvider.Value];
                switch(user.TwoFactorProvider.Value)
                {
                    case TwoFactorProviderType.Authenticator:
                        AuthenticatorKey = provider.MetaData["Key"];
                        break;
                    default:
                        break;
                }
            }
            else
            {
                TwoFactorEnabled = false;
            }

            TwoFactorEnabled = user.TwoFactorIsEnabled();
            TwoFactorProvider = user.TwoFactorProvider;
            TwoFactorRecoveryCode = user.TwoFactorRecoveryCode;
        }

        public bool TwoFactorEnabled { get; set; }
        public TwoFactorProviderType? TwoFactorProvider { get; set; }
        public string AuthenticatorKey { get; set; }
        public string TwoFactorRecoveryCode { get; set; }
    }
}
