using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Response.Organizations;

public class OrganizationUserResponseModel : ResponseModel
{
    public OrganizationUserResponseModel(OrganizationUser organizationUser, string obj = "organizationUser")
        : base(obj)
    {
        if (organizationUser == null)
        {
            throw new ArgumentNullException(nameof(organizationUser));
        }

        Id = organizationUser.Id.ToString();
        UserId = organizationUser.UserId?.ToString();
        Type = organizationUser.Type;
        Status = organizationUser.Status;
        AccessAll = organizationUser.AccessAll;
        Permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(organizationUser.Permissions);
        ResetPasswordEnrolled = !string.IsNullOrEmpty(organizationUser.ResetPasswordKey);
    }

    public OrganizationUserResponseModel(OrganizationUserUserDetails organizationUser, string obj = "organizationUser")
        : base(obj)
    {
        if (organizationUser == null)
        {
            throw new ArgumentNullException(nameof(organizationUser));
        }

        Id = organizationUser.Id.ToString();
        UserId = organizationUser.UserId?.ToString();
        Type = organizationUser.Type;
        Status = organizationUser.Status;
        AccessAll = organizationUser.AccessAll;
        Permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(organizationUser.Permissions);
        ResetPasswordEnrolled = !string.IsNullOrEmpty(organizationUser.ResetPasswordKey);
        UsesKeyConnector = organizationUser.UsesKeyConnector;
    }

    public string Id { get; set; }
    public string UserId { get; set; }
    public OrganizationUserType Type { get; set; }
    public OrganizationUserStatusType Status { get; set; }
    public bool AccessAll { get; set; }
    public Permissions Permissions { get; set; }
    public bool ResetPasswordEnrolled { get; set; }
    public bool UsesKeyConnector { get; set; }
}

public class OrganizationUserDetailsResponseModel : OrganizationUserResponseModel
{
    public OrganizationUserDetailsResponseModel(OrganizationUser organizationUser,
        IEnumerable<SelectionReadOnly> collections)
        : base(organizationUser, "organizationUserDetails")
    {
        Collections = collections.Select(c => new SelectionReadOnlyResponseModel(c));
    }

    public IEnumerable<SelectionReadOnlyResponseModel> Collections { get; set; }
}

public class OrganizationUserUserDetailsResponseModel : OrganizationUserResponseModel
{
    public OrganizationUserUserDetailsResponseModel(OrganizationUserUserDetails organizationUser,
        bool twoFactorEnabled, string obj = "organizationUserUserDetails")
        : base(organizationUser, obj)
    {
        if (organizationUser == null)
        {
            throw new ArgumentNullException(nameof(organizationUser));
        }

        Name = organizationUser.Name;
        Email = organizationUser.Email;
        TwoFactorEnabled = twoFactorEnabled;
        SsoBound = !string.IsNullOrWhiteSpace(organizationUser.SsoExternalId);
        // Prevent reset password when using key connector.
        ResetPasswordEnrolled = ResetPasswordEnrolled && !organizationUser.UsesKeyConnector;
    }

    public string Name { get; set; }
    public string Email { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool SsoBound { get; set; }
}

public class OrganizationUserResetPasswordDetailsResponseModel : ResponseModel
{
    public OrganizationUserResetPasswordDetailsResponseModel(OrganizationUserResetPasswordDetails orgUser,
        string obj = "organizationUserResetPasswordDetails") : base(obj)
    {
        if (orgUser == null)
        {
            throw new ArgumentNullException(nameof(orgUser));
        }

        Kdf = orgUser.Kdf;
        KdfIterations = orgUser.KdfIterations;
        ResetPasswordKey = orgUser.ResetPasswordKey;
        EncryptedPrivateKey = orgUser.EncryptedPrivateKey;
    }

    public KdfType Kdf { get; set; }
    public int KdfIterations { get; set; }
    public string ResetPasswordKey { get; set; }
    public string EncryptedPrivateKey { get; set; }
}

public class OrganizationUserPublicKeyResponseModel : ResponseModel
{
    public OrganizationUserPublicKeyResponseModel(Guid id, Guid userId,
        string key, string obj = "organizationUserPublicKeyResponseModel") :
        base(obj)
    {
        Id = id;
        UserId = userId;
        Key = key;
    }

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Key { get; set; }
}

public class OrganizationUserBulkResponseModel : ResponseModel
{
    public OrganizationUserBulkResponseModel(Guid id, string error,
        string obj = "OrganizationBulkConfirmResponseModel") : base(obj)
    {
        Id = id;
        Error = error;
    }
    public Guid Id { get; set; }
    public string Error { get; set; }
}
