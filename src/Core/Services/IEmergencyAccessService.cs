using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.Services;

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
    Task<ICollection<Policy>> GetPoliciesAsync(Guid id, User requestingUser);
    Task<(EmergencyAccess, User)> TakeoverAsync(Guid id, User initiatingUser);
    Task PasswordAsync(Guid id, User user, string newMasterPasswordHash, string key);
    Task SendNotificationsAsync();
    Task HandleTimedOutRequestsAsync();
    Task<EmergencyAccessViewData> ViewAsync(Guid id, User user);
    Task<AttachmentResponseData> GetAttachmentDownloadAsync(Guid id, Guid cipherId, string attachmentId, User user);
}
