﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Models.Data.Organizations.OrganizationUsers;

public class OrganizationUserResetPasswordDetails
{
    public OrganizationUserResetPasswordDetails() { }

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

        OrganizationUserId = orgUser.Id;
        Kdf = user.Kdf;
        KdfIterations = user.KdfIterations;
        KdfMemory = user.KdfMemory;
        KdfParallelism = user.KdfParallelism;
        ResetPasswordKey = orgUser.ResetPasswordKey;
        EncryptedPrivateKey = org.PrivateKey;
    }
    public Guid OrganizationUserId { get; set; }
    public KdfType Kdf { get; set; }
    public int KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }
    public string ResetPasswordKey { get; set; }
    public string EncryptedPrivateKey { get; set; }
}
