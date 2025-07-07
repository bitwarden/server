﻿using System.ComponentModel.DataAnnotations;
using Bit.Api.Models.Request;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request.Organizations;

public class OrganizationUserInviteRequestModel
{
    [Required]
    [StrictEmailAddressList]
    public IEnumerable<string> Emails { get; set; }
    [Required]
    [EnumDataType(typeof(OrganizationUserType))]
    public OrganizationUserType? Type { get; set; }
    public bool AccessSecretsManager { get; set; }
    public Permissions Permissions { get; set; }
    public IEnumerable<SelectionReadOnlyRequestModel> Collections { get; set; }
    public IEnumerable<Guid> Groups { get; set; }

    public OrganizationUserInviteData ToData()
    {
        return new OrganizationUserInviteData
        {
            Emails = Emails,
            Type = Type,
            AccessSecretsManager = AccessSecretsManager,
            Collections = Collections?.Select(c => c.ToSelectionReadOnly()),
            Groups = Groups,
            Permissions = Permissions,
        };
    }
}

public class OrganizationUserAcceptInitRequestModel
{
    [Required]
    public string Token { get; set; }
    [Required]
    public string Key { get; set; }
    [Required]
    public OrganizationKeysRequestModel Keys { get; set; }
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string CollectionName { get; set; }
}

public class OrganizationUserAcceptRequestModel
{
    [Required]
    public string Token { get; set; }
    // Used to auto-enroll in master password reset
    public string ResetPasswordKey { get; set; }
}

public class OrganizationUserConfirmRequestModel
{
    [Required]
    public string Key { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string DefaultUserCollectionName { get; set; }
}

public class OrganizationUserBulkConfirmRequestModelEntry
{
    [Required]
    public Guid Id { get; set; }
    [Required]
    public string Key { get; set; }
}

public class OrganizationUserBulkConfirmRequestModel
{
    [Required]
    public IEnumerable<OrganizationUserBulkConfirmRequestModelEntry> Keys { get; set; }

    public Dictionary<Guid, string> ToDictionary()
    {
        return Keys.ToDictionary(e => e.Id, e => e.Key);
    }
}

public class OrganizationUserUpdateRequestModel
{
    [Required]
    [EnumDataType(typeof(OrganizationUserType))]
    public OrganizationUserType? Type { get; set; }
    public bool AccessSecretsManager { get; set; }
    public Permissions Permissions { get; set; }
    public IEnumerable<SelectionReadOnlyRequestModel> Collections { get; set; }
    public IEnumerable<Guid> Groups { get; set; }

    public OrganizationUser ToOrganizationUser(OrganizationUser existingUser)
    {
        existingUser.Type = Type.Value;
        existingUser.Permissions = CoreHelpers.ClassToJsonData(Permissions);
        existingUser.AccessSecretsManager = AccessSecretsManager;
        return existingUser;
    }
}

public class OrganizationUserResetPasswordEnrollmentRequestModel
{
    public string ResetPasswordKey { get; set; }
    public string MasterPasswordHash { get; set; }
}

public class OrganizationUserBulkRequestModel
{
    [Required]
    public IEnumerable<Guid> Ids { get; set; }
}

public class ResetPasswordWithOrgIdRequestModel : OrganizationUserResetPasswordEnrollmentRequestModel
{
    [Required]
    public Guid OrganizationId { get; set; }
}
