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
    /// <summary>
    /// Invites a user via email to become an emergency contact for the Grantor user. The Grantor must have a premium subscription.
    /// the grantor user must not be a member of the organization that uses KeyConnector.
    /// </summary>
    /// <param name="grantorUser">The user initiating the emergency contact request</param>
    /// <param name="emergencyContactEmail">Emergency contact</param>
    /// <param name="accessType">Type of emergency access allowed to the emergency contact</param>
    /// <param name="waitTime">The amount of time to pass before the invite is auto confirmed</param>
    /// <returns>a new Emergency Access object</returns>
    Task<EmergencyAccess> InviteAsync(User grantorUser, string emergencyContactEmail, EmergencyAccessType accessType, int waitTime);
    /// <summary>
    /// Sends an invite to the emergency contact associated with the emergency access id.
    /// </summary>
    /// <param name="grantorUser">The grantor. This must be the owner of the Emergency Access object</param>
    /// <param name="emergencyAccessId">The Id of the emergency access being requested.</param>
    /// <returns>void</returns>
    Task ResendInviteAsync(User grantorUser, Guid emergencyAccessId);
    /// <summary>
    /// A grantee user accepts the emergency contact request. This updates the emergency access status to be
    /// "Accepted", this is the middle step before the grantor user confirms the request.
    /// </summary>
    /// <param name="emergencyAccessId">Id of the emergency access object being acted on.</param>
    /// <param name="granteeUser">User being invited to be an emergency contact</param>
    /// <param name="token">the tokenable that was sent via email</param>
    /// <param name="userService">service dependency</param>
    /// <returns>void</returns>
    Task<EmergencyAccess> AcceptUserAsync(Guid emergencyAccessId, User granteeUser, string token, IUserService userService);
    /// <summary>
    /// The creator of the emergency access request can delete the request.
    /// </summary>
    /// <param name="emergencyAccessId">Id of the emergency access being acted on</param>
    /// <param name="grantorId">Id of the owner trying to delete the emergency access request</param>
    /// <returns>void</returns>
    Task DeleteAsync(Guid emergencyAccessId, Guid grantorId);
    /// <summary>
    /// The grantor user confirms the acceptance of the emergency contact request. This stores the encrypted key allowing the grantee
    /// access based on the emergency access type.
    /// </summary>
    /// <param name="emergencyAccessId">Id of the emergency access being acted on.</param>
    /// <param name="key">The grantor user key encrypted by the grantee public key; grantee.PubicKey(grantor.User.Key)</param>
    /// <param name="grantorId">Id of grantor user</param>
    /// <returns>emergency access object associated with the Id passed in</returns>
    Task<EmergencyAccess> ConfirmUserAsync(Guid emergencyAccessId, string key, Guid grantorId);
    /// <summary>
    /// Fetches an emergency access object. The grantor user must own the object being fetched.
    /// </summary>
    /// <param name="emergencyAccessId">Id of emergency access object</param>
    /// <param name="grantorId">Id of the owner of the emergency access object.</param>
    /// <returns>Details of the emergency access object</returns>
    Task<EmergencyAccessDetails> GetAsync(Guid emergencyAccessId, Guid grantorId);
    /// <summary>
    /// Updates the emergency access object.
    /// </summary>
    /// <param name="emergencyAccess">emergency access entity being updated</param>
    /// <param name="grantorUser">grantor user</param>
    /// <returns>void</returns>
    Task SaveAsync(EmergencyAccess emergencyAccess, User grantorUser);
    /// <summary>
    /// Initiates the recovery process. For either Takeover or view. Will send an email to the Grantor User notifying of the initiation.
    /// </summary>
    /// <param name="emergencyAccessId">EmergencyAccess Id</param>
    /// <param name="granteeUser">grantee user</param>
    /// <returns>void</returns>
    Task InitiateAsync(Guid emergencyAccessId, User granteeUser);
    /// <summary>
    /// Approves a recovery request. Sets the EmergencyAccess.Status to RecoveryApproved.
    /// </summary>
    /// <param name="emergencyAccessId">emergency access id</param>
    /// <param name="grantorUser">grantor user</param>
    /// <returns>void</returns>
    Task ApproveAsync(Guid emergencyAccessId, User grantorUser);
    /// <summary>
    /// Rejects a recovery request. Sets the EmergencyAccess.Status to Confirmed. This does not remove the emergency access entity. The
    /// Grantee user can still initiate another recovery request.
    /// </summary>
    /// <param name="emergencyAccessId">emergency access id</param>
    /// <param name="grantorUser">grantor user</param>
    /// <returns>void</returns>
    Task RejectAsync(Guid emergencyAccessId, User grantorUser);
    /// <summary>
    /// This request is made by the Grantee user to fetch the policies <see cref="Policy"/> for the Grantor User.
    /// The Grantor User has to be the owner of the organization. <see cref="OrganizationUserType"/>
    /// If the Grantor user has OrganizationUserType.Owner then the policies for the _Grantor_ user
    /// are returned. This is used to ensure the password is of the proper complexity for the organization.
    /// </summary>
    /// <param name="emergencyAccessId">EmergencyAccess.Id being acted on</param>
    /// <param name="granteeUser">User making the request, this is the Grantee</param>
    /// <returns>null if the GrantorUser is not an organization owner; A list of policies otherwise.</returns>
    Task<ICollection<Policy>> GetPoliciesAsync(Guid emergencyAccessId, User granteeUser);
    /// <summary>
    /// Fetches the emergency access entity and grantor user. The grantor user is returned so the correct KDF configuration is
    /// used for the new password.
    /// </summary>
    /// <param name="emergencyAccessId">Id of entity being accessed</param>
    /// <param name="granteeUser">grantee user of the emergency access entity</param>
    /// <returns>emergency access entity and the grantorUser</returns>
    Task<(EmergencyAccess, User)> TakeoverAsync(Guid emergencyAccessId, User granteeUser);
    /// <summary>
    /// Updates the grantor's password hash and updates the key for the EmergencyAccess entity.
    /// </summary>
    /// <param name="emergencyAccessId">Emergency Access Id being acted on</param>
    /// <param name="granteeUser">user making the request</param>
    /// <param name="newMasterPasswordHash">new password hash set by grantee user</param>
    /// <param name="key">new encrypted user key</param>
    /// <returns>void</returns>
    Task PasswordAsync(Guid emergencyAccessId, User granteeUser, string newMasterPasswordHash, string key);
    /// <summary>
    /// sends a reminder email that there is a pending request for recovery.
    /// </summary>
    /// <returns>void</returns>
    Task SendNotificationsAsync();
    /// <summary>
    /// This handles the auto approval of recovery requests started in the <see cref="InitiateAsync"/> method.
    /// An email will be sent to the Grantee and the Grantor notifying each the recovery has been approved.
    /// </summary>
    /// <returns>void</returns>
    Task HandleTimedOutRequestsAsync();
    /// <summary>
    /// Fetched ciphers from the grantors account for the grantee to view.
    /// </summary>
    /// <param name="emergencyAccessId">Emergency access entity being acted on</param>
    /// <param name="granteeUser">user requesting cipher items</param>
    /// <returns>ciphers associated with the emergency access request</returns>
    Task<EmergencyAccessViewData> ViewAsync(Guid emergencyAccessId, User granteeUser);
    /// <summary>
    /// Returns attachment if the grantee user has access to the cipher through the emergency access entity.
    /// </summary>
    /// <param name="emergencyAccessId">EmergencyAccess entity being acted on</param>
    /// <param name="cipherId">cipher entity containing the attachment</param>
    /// <param name="attachmentId">Attachment entity</param>
    /// <param name="granteeUser">user making the request</param>
    /// <returns>attachment response </returns>
    Task<AttachmentResponseData> GetAttachmentDownloadAsync(Guid emergencyAccessId, Guid cipherId, string attachmentId, User granteeUser);
}
