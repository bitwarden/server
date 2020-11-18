using System;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Api.Response;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface IEmergencyAccessService
    {
        Task<EmergencyAccess> InviteAsync(Guid invitingUserId, string invitingUsersName, string email, EmergencyAccessType type, int waitTime);
        Task ResendInviteAsync(Guid invitingUserId, Guid emergencyAccessId, string invitingUsersName);
        Task<EmergencyAccess> AcceptUserAsync(Guid emergencyAccessId, User user, string token, IUserService userService);
        Task DeleteAsync(Guid emergencyAccessId, Guid grantorId);
        Task<EmergencyAccess> ConfirmUserAsync(Guid emergencyAccessId, string key, Guid grantorId);
        Task<EmergencyAccessDetails> GetAsync(Guid emergencyAccessId, Guid userId);
        Task SaveAsync(EmergencyAccess emergencyAccess, Guid savingUserId);
        Task InitiateAsync(Guid id, User initiatingUser);
        Task ApproveAsync(Guid id, User approvingUser);
        Task RejectAsync(Guid id, User rejectingUser);
        Task<(EmergencyAccess, User)> TakeoverAsync(Guid id, User initiatingUser);
        Task PasswordAsync(Guid id, User user, string newMasterPasswordHash, string key);
        Task SendNotificationsAsync();
        Task HandleTimedOutRequestsAsync();
        Task<EmergencyAccessViewResponseModel> ViewAsync(Guid id, User user);
    }
}
