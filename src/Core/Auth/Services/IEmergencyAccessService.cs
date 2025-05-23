using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Auth.Services;

public interface IEmergencyAccessService
{
    Task<EmergencyAccess> InviteAsync(User invitingUser, string email, EmergencyAccessType type, int waitTime);
    Task ResendInviteAsync(User invitingUser, Guid emergencyAccessId);
    Task<EmergencyAccess> AcceptUserAsync(Guid emergencyAccessId, User user, string token, IUserService userService);
    Task DeleteAsync(Guid emergencyAccessId, Guid grantorId);
    Task<EmergencyAccess> ConfirmUserAsync(Guid emergencyAccessId, string key, Guid grantorId);
    Task<EmergencyAccessDetails> GetAsync(Guid emergencyAccessId, Guid userId);
    Task SaveAsync(EmergencyAccess emergencyAccess, User savingUser);
    Task InitiateAsync(Guid id, User initiatingUser);
    Task ApproveAsync(Guid id, User approvingUser);
    Task RejectAsync(Guid id, User rejectingUser);
    /// <summary>
    /// This request is made by the Grantee user to fetch the policies <see cref="Policy"/> for the Grantor User.
    /// The Grantor User has to be the owner of the organization. <see cref="OrganizationUserType"/>
    /// If the Grantor user has OrganizationUserType.Owner then the policies for the _Grantor_ user
    /// are returned.
    /// </summary>
    /// <param name="id">EmergencyAccess.Id being acted on</param>
    /// <param name="requestingUser">User making the request, this is the Grantee</param>
    /// <returns>null if the GrantorUser is not an organization owner; A list of policies otherwise.</returns>
    Task<ICollection<Policy>> GetPoliciesAsync(Guid id, User requestingUser);
    Task<(EmergencyAccess, User)> TakeoverAsync(Guid id, User initiatingUser);
    Task PasswordAsync(Guid id, User user, string newMasterPasswordHash, string key);
    Task SendNotificationsAsync();
    Task HandleTimedOutRequestsAsync();
    Task<EmergencyAccessViewData> ViewAsync(Guid id, User user);
    Task<AttachmentResponseData> GetAttachmentDownloadAsync(Guid id, Guid cipherId, string attachmentId, User user);
}
