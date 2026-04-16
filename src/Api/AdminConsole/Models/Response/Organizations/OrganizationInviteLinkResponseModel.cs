using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationInviteLinkResponseModel : ResponseModel
{
    public OrganizationInviteLinkResponseModel() : base("organizationInviteLink")
    {
    }

    public OrganizationInviteLinkResponseModel(OrganizationInviteLink inviteLink)
        : base("organizationInviteLink")
    {
        ArgumentNullException.ThrowIfNull(inviteLink);

        Id = inviteLink.Id;
        Code = inviteLink.Code;
        OrganizationId = inviteLink.OrganizationId;
        AllowedDomains = inviteLink.GetAllowedDomains();
        EncryptedInviteKey = inviteLink.EncryptedInviteKey;
        EncryptedOrgKey = inviteLink.EncryptedOrgKey;
        CreationDate = inviteLink.CreationDate;
    }

    public Guid Id { get; set; }
    public Guid Code { get; set; }
    public Guid OrganizationId { get; set; }
    public IEnumerable<string> AllowedDomains { get; set; } = [];
    public string EncryptedInviteKey { get; set; } = null!;
    public string? EncryptedOrgKey { get; set; }
    public DateTime CreationDate { get; set; }
}
