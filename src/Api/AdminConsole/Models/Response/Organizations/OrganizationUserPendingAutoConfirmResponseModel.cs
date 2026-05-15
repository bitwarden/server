using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationUserPendingAutoConfirmResponseModel : ResponseModel
{
    public OrganizationUserPendingAutoConfirmResponseModel(OrganizationUser organizationUser)
        : base("OrganizationUserPendingAutoConfirmResponseModel")
    {
        Id = organizationUser.Id;
        UserId = organizationUser.UserId
            ?? throw new InvalidOperationException($"OrganizationUser {organizationUser.Id} has no associated UserId.");
    }

    /// <summary>The OrganizationUser ID.</summary>
    public Guid Id { get; set; }

    /// <summary>The User ID.</summary>
    public Guid UserId { get; set; }
}
