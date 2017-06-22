using System;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class TwoFactorU2fResponseModel : ResponseModel
    {
        public TwoFactorU2fResponseModel(User user)
            : base("twoFactorU2f")
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.U2f);
            if(provider?.MetaData != null && provider.MetaData.Count > 0)
            {
                Challenge = new ChallengeModel
                {
                    // TODO
                };
                Enabled = provider.Enabled;
            }
            else
            {
                Enabled = false;
            }
        }

        public ChallengeModel Challenge { get; set; }
        public bool Enabled { get; set; }

        public class ChallengeModel
        {
            public string UserId { get; set; }
            public string AppId { get; set; }
            public string Challenge { get; set; }
            public string Version { get; set; }
        }
    }
}
