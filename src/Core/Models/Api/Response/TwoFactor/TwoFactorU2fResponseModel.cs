using System;
using Bit.Core.Models.Table;
using Bit.Core.Models.Business;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api
{
    public class TwoFactorU2fResponseModel : ResponseModel
    {
        public TwoFactorU2fResponseModel(User user, TwoFactorProvider provider, U2fRegistration registration = null)
            : base("twoFactorU2f")
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if(registration != null)
            {
                Challenge = new ChallengeModel(user, registration);
            }
            Enabled = provider.Enabled;
        }

        public TwoFactorU2fResponseModel(User user)
            : base("twoFactorU2f")
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.U2f);
            Enabled = provider != null && provider.Enabled;
        }

        public ChallengeModel Challenge { get; set; }
        public bool Enabled { get; set; }

        public class ChallengeModel
        {
            public ChallengeModel(User user, U2fRegistration registration)
            {
                UserId = user.Id.ToString();
                AppId = registration.AppId;
                Challenge = registration.Challenge;
                Version = registration.Version;
            }

            public string UserId { get; set; }
            public string AppId { get; set; }
            public string Challenge { get; set; }
            public string Version { get; set; }
        }
    }
}
