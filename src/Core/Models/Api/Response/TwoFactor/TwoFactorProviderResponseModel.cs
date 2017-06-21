using System;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class TwoFactorProviderResponseModel : ResponseModel
    {
        public TwoFactorProviderResponseModel(TwoFactorProviderType type, TwoFactorProvider provider)
            : base("twoFactorProvider")
        {
            if(provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            Enabled = provider.Enabled;
            Type = type;
        }

        public TwoFactorProviderResponseModel(TwoFactorProviderType type, User user)
            : base("twoFactorProvider")
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var provider = user.GetTwoFactorProvider(type);
            Enabled = provider?.Enabled ?? false;
            Type = type;
        }

        public bool Enabled { get; set; }
        public TwoFactorProviderType Type { get; set; }
    }
}
