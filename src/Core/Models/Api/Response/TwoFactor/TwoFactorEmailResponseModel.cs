using System;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class TwoFactorEmailResponseModel : ResponseModel
    {
        public TwoFactorEmailResponseModel(User user)
            : base("twoFactorEmail")
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Email);
            if(provider?.MetaData?.ContainsKey("Email") ?? false)
            {
                Email = provider.MetaData["Email"];
                Enabled = provider.Enabled;
            }
            else
            {
                Enabled = false;
            }
        }

        public bool Enabled { get; set; }
        public string Email { get; set; }
    }
}
