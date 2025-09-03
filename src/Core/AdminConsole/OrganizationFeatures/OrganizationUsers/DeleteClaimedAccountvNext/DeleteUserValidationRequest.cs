using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccountvNext;

public class DeleteUserValidationRequest
{
    public Guid OrganizationId { get; init; }
    public Guid OrganizationUserId { get; init; }
    public OrganizationUser? OrganizationUser { get; init; }
    public User? User { get; init; }
    public Guid DeletingUserId { get; init; }
    public bool IsClaimed { get; init; }
}
