﻿using System.Text.Json.Serialization;
using Bit.Api.Models.Response;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationUserResponseModel : ResponseModel
{
    public OrganizationUserResponseModel(OrganizationUser organizationUser, string obj = "organizationUser")
        : base(obj)
    {
        if (organizationUser == null)
        {
            throw new ArgumentNullException(nameof(organizationUser));
        }

        Id = organizationUser.Id;
        UserId = organizationUser.UserId;
        Type = organizationUser.Type;
        Status = organizationUser.Status;
        ExternalId = organizationUser.ExternalId;
        AccessSecretsManager = organizationUser.AccessSecretsManager;
        Permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(organizationUser.Permissions);
        ResetPasswordEnrolled = !string.IsNullOrEmpty(organizationUser.ResetPasswordKey);
    }

    public OrganizationUserResponseModel(OrganizationUserUserDetails organizationUser,
        string obj = "organizationUser")
        : base(obj)
    {
        if (organizationUser == null)
        {
            throw new ArgumentNullException(nameof(organizationUser));
        }

        Id = organizationUser.Id;
        UserId = organizationUser.UserId;
        Type = organizationUser.Type;
        Status = organizationUser.Status;
        ExternalId = organizationUser.ExternalId;
        AccessSecretsManager = organizationUser.AccessSecretsManager;
        Permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(organizationUser.Permissions);
        ResetPasswordEnrolled = !string.IsNullOrEmpty(organizationUser.ResetPasswordKey);
        UsesKeyConnector = organizationUser.UsesKeyConnector;
        HasMasterPassword = organizationUser.HasMasterPassword;
    }

    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public OrganizationUserType Type { get; set; }
    public OrganizationUserStatusType Status { get; set; }
    public string ExternalId { get; set; }
    public bool AccessSecretsManager { get; set; }
    public Permissions Permissions { get; set; }
    public bool ResetPasswordEnrolled { get; set; }
    public bool UsesKeyConnector { get; set; }
    public bool HasMasterPassword { get; set; }
}

public class OrganizationUserDetailsResponseModel : OrganizationUserResponseModel
{
    public OrganizationUserDetailsResponseModel(
        OrganizationUser organizationUser,
        bool managedByOrganization,
        IEnumerable<CollectionAccessSelection> collections)
        : base(organizationUser, "organizationUserDetails")
    {
        ManagedByOrganization = managedByOrganization;
        Collections = collections.Select(c => new SelectionReadOnlyResponseModel(c));
    }

    public OrganizationUserDetailsResponseModel(OrganizationUserUserDetails organizationUser,
        bool managedByOrganization,
        IEnumerable<CollectionAccessSelection> collections)
        : base(organizationUser, "organizationUserDetails")
    {
        ManagedByOrganization = managedByOrganization;
        Collections = collections.Select(c => new SelectionReadOnlyResponseModel(c));
    }

    public bool ManagedByOrganization { get; set; }

    public IEnumerable<SelectionReadOnlyResponseModel> Collections { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<Guid> Groups { get; set; }
}

#nullable enable
public class OrganizationUserUserMiniDetailsResponseModel : ResponseModel
{
    public OrganizationUserUserMiniDetailsResponseModel(OrganizationUserUserDetails organizationUser)
        : base("organizationUserUserMiniDetails")
    {
        Id = organizationUser.Id;
        UserId = organizationUser.UserId;
        Type = organizationUser.Type;
        Status = organizationUser.Status;
        Name = organizationUser.Name;
        Email = organizationUser.Email;
    }

    public Guid Id { get; }
    public Guid? UserId { get; }
    public OrganizationUserType Type { get; }
    public OrganizationUserStatusType Status { get; }
    public string? Name { get; }
    public string Email { get; }
}
#nullable disable

public class OrganizationUserUserDetailsResponseModel : OrganizationUserResponseModel
{
    public OrganizationUserUserDetailsResponseModel(OrganizationUserUserDetails organizationUser,
        bool twoFactorEnabled, bool managedByOrganization, string obj = "organizationUserUserDetails")
        : base(organizationUser, obj)
    {
        if (organizationUser == null)
        {
            throw new ArgumentNullException(nameof(organizationUser));
        }

        Name = organizationUser.Name;
        Email = organizationUser.Email;
        AvatarColor = organizationUser.AvatarColor;
        TwoFactorEnabled = twoFactorEnabled;
        SsoBound = !string.IsNullOrWhiteSpace(organizationUser.SsoExternalId);
        Collections = organizationUser.Collections.Select(c => new SelectionReadOnlyResponseModel(c));
        Groups = organizationUser.Groups;
        // Prevent reset password when using key connector.
        ResetPasswordEnrolled = ResetPasswordEnrolled && !organizationUser.UsesKeyConnector;
        ManagedByOrganization = managedByOrganization;
    }

    public string Name { get; set; }
    public string Email { get; set; }
    public string AvatarColor { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool SsoBound { get; set; }
    /// <summary>
    /// Indicates if the organization manages the user. If a user is "managed" by an organization,
    /// the organization has greater control over their account, and some user actions are restricted.
    /// </summary>
    public bool ManagedByOrganization { get; set; }
    public IEnumerable<SelectionReadOnlyResponseModel> Collections { get; set; }
    public IEnumerable<Guid> Groups { get; set; }
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

        OrganizationUserId = orgUser.OrganizationUserId;
        Kdf = orgUser.Kdf;
        KdfIterations = orgUser.KdfIterations;
        KdfMemory = orgUser.KdfMemory;
        KdfParallelism = orgUser.KdfParallelism;
        ResetPasswordKey = orgUser.ResetPasswordKey;
        EncryptedPrivateKey = orgUser.EncryptedPrivateKey;
    }

    public Guid OrganizationUserId { get; set; }
    public KdfType Kdf { get; set; }
    public int KdfIterations { get; set; }
    public int? KdfMemory { get; set; }
    public int? KdfParallelism { get; set; }
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
