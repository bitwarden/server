using System;
using Bit.Core.Enums;

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

        public bool Enabled { get; set; }
        public TwoFactorProviderType Type { get; set; }
    }
}
