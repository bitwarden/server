using System;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Data
{
    public class OrganizationUserResetPasswordDetails
    {
        public OrganizationUserResetPasswordDetails(OrganizationUser orgUser, User user, Organization org)
        {
            if (orgUser == null)
            {
                throw new ArgumentNullException(nameof(orgUser));
            }

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (org == null)
            {
                throw new ArgumentNullException(nameof(org));
            }

            Kdf = user.Kdf;
            KdfIterations = user.KdfIterations;
            ResetPasswordKey = orgUser.ResetPasswordKey;
            EncryptedPrivateKey = org.PrivateKey;
        }
        public KdfType Kdf { get; set; }
        public int KdfIterations { get; set; }
        public string ResetPasswordKey { get; set; }
        public string EncryptedPrivateKey { get; set; }
    }
}
