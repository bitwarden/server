using System;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Data
{
    public class OrganizationUserResetPasswordDetails
    {
        public OrganizationUserResetPasswordDetails(OrganizationUser orgUser, User user)
        {
            if (orgUser == null)
            {
                throw new ArgumentNullException(nameof(orgUser));
            }
            
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            Kdf = user.Kdf;
            KdfIterations = user.KdfIterations;
            ResetPasswordKey = orgUser.ResetPasswordKey;
        }
        public KdfType Kdf { get; set; }
        public int KdfIterations { get; set; }
        public string ResetPasswordKey { get; set; }
    }
}
