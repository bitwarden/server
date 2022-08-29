using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Api.Models.Request.Accounts;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationUserInviteRequestModel
{
    [Required]
    [StrictEmailAddressList]
    public IEnumerable<string> Emails { get; set; }
    [Required]
    public OrganizationUserType? Type { get; set; }
    public bool AccessAll { get; set; }
    public Permissions Permissions { get; set; }
    public IEnumerable<SelectionReadOnlyRequestModel> Collections { get; set; }

    public OrganizationUserInviteData ToData()
    {
        return new OrganizationUserInviteData
        {
            Emails = Emails,
            Type = Type,
            AccessAll = AccessAll,
            Collections = Collections?.Select(c => c.ToSelectionReadOnly()),
            Permissions = Permissions,
        };
    }
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
    public OrganizationUserType? Type { get; set; }
    public bool AccessAll { get; set; }
    public Permissions Permissions { get; set; }
    public IEnumerable<SelectionReadOnlyRequestModel> Collections { get; set; }

    public OrganizationUser ToOrganizationUser(OrganizationUser existingUser)
    {
        existingUser.Type = Type.Value;
        existingUser.Permissions = JsonSerializer.Serialize(Permissions, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        existingUser.AccessAll = AccessAll;
        return existingUser;
    }
}

public class OrganizationUserUpdateGroupsRequestModel
{
    [Required]
    public IEnumerable<string> GroupIds { get; set; }
}

public class OrganizationUserResetPasswordEnrollmentRequestModel : SecretVerificationRequestModel
{
    public string ResetPasswordKey { get; set; }
}

public class OrganizationUserBulkRequestModel
{
    [Required]
    public IEnumerable<Guid> Ids { get; set; }
}
