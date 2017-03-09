using System;
using Bit.Core.Models.Table;
using Bit.Core.Enums;

namespace Bit.Api.Models
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

            TwoFactorEnabled = user.TwoFactorEnabled;
            AuthenticatorKey = user.AuthenticatorKey;
            TwoFactorProvider = user.TwoFactorProvider;
            TwoFactorRecoveryCode = user.TwoFactorRecoveryCode;
        }

        public bool TwoFactorEnabled { get; set; }
        public TwoFactorProviderType? TwoFactorProvider { get; set; }
        public string AuthenticatorKey { get; set; }
        public string TwoFactorRecoveryCode { get; set; }
    }
}
