using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Models.Data.Organizations.OrganizationUsers;

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
        KdfMemory = user.KdfMemory;
        KdfParallelism = user.KdfParallelism;
        ResetPasswordKey = orgUser.ResetPasswordKey;
        EncryptedPrivateKey = org.PrivateKey;
    }
    public KdfType Kdf { get; set; }
    public int KdfIterations { get; set; }
    public int KdfMemory { get; set; }
    public int KdfParallelism { get; set; }
    public string ResetPasswordKey { get; set; }
    public string EncryptedPrivateKey { get; set; }
}
