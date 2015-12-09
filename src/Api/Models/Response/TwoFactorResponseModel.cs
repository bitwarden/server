using System;
using Bit.Core.Domains;
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
        }

        public bool TwoFactorEnabled { get; set; }
        public TwoFactorProvider? TwoFactorProvider { get; set; }
        public string AuthenticatorKey { get; set; }
    }
}
