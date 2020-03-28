using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class TwoFactorRecoverResponseModel : ResponseModel
    {
        public TwoFactorRecoverResponseModel(User user)
            : base("twoFactorRecover")
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            Code = user.TwoFactorRecoveryCode;
        }

        public string Code { get; set; }
    }
}
