using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Seeds an emergency access contact for an existing user (the grantor), in the requested
/// <see cref="EmergencyAccessStatusType"/>. The row mirrors what the real invite, accept, confirm and
/// recovery flows leave behind:
/// <list type="bullet">
/// <item><see cref="EmergencyAccessStatusType.Invited"/>: <c>Email</c> set, no grantee.</item>
/// <item><see cref="EmergencyAccessStatusType.Accepted"/>: <c>GranteeId</c> set, <c>Email</c> cleared.</item>
/// <item><see cref="EmergencyAccessStatusType.Confirmed"/>: adds <c>KeyEncrypted</c>, the grantor's user key wrapped to the grantee's public key.</item>
/// <item><see cref="EmergencyAccessStatusType.RecoveryInitiated"/>: adds the recovery and notification dates.</item>
/// <item><see cref="EmergencyAccessStatusType.RecoveryApproved"/>: recovery initiated far enough in the past that the wait time has elapsed.</item>
/// </list>
/// Use <c>EmergencyAccessInviteQuery</c> to retrieve the accept-invite URL of an <c>Invited</c> contact.
/// </summary>
public class UserEmergencyAccessContactScene(
    IUserRepository userRepository,
    IEmergencyAccessRepository emergencyAccessRepository,
    IManglerService manglerService) : IScene<UserEmergencyAccessContactScene.Request, UserEmergencyAccessContactScene.Result>
{
    public class Request
    {
        /// <summary>
        /// The user who grants emergency access to their vault.
        /// </summary>
        [Required]
        public required Guid GrantorUserId { get; set; }
        /// <summary>
        /// The status to seed the emergency access contact in.
        /// </summary>
        [Required]
        public required EmergencyAccessStatusType Status { get; set; }
        /// <summary>
        /// The email address of the invited contact. Required for <see cref="EmergencyAccessStatusType.Invited"/>,
        /// ignored for later statuses (the grantee's account is used instead).
        /// </summary>
        [EmailAddress]
        [MaxLength(256)]
        public string? Email { get; set; }
        /// <summary>
        /// The existing user acting as the emergency contact (grantee). Required for every status after
        /// <see cref="EmergencyAccessStatusType.Invited"/>.
        /// </summary>
        public Guid? GranteeUserId { get; set; }
        /// <summary>
        /// The grantor's decrypted user key (base64). Required from <see cref="EmergencyAccessStatusType.Confirmed"/> on,
        /// because it is wrapped to the grantee's public key.
        /// </summary>
        public string? GrantorUserKeyB64 { get; set; }
        public EmergencyAccessType Type { get; set; } = EmergencyAccessType.View;
        [Range(1, 90)]
        public short WaitTimeDays { get; set; } = 7;
    }

    public class Result
    {
        public Guid EmergencyAccessId { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        if (!Enum.IsDefined(request.Status))
        {
            throw new ArgumentException($"Unknown emergency access status {request.Status}.");
        }

        var grantor = await userRepository.GetByIdAsync(request.GrantorUserId);
        if (grantor == null)
        {
            throw new Exception($"User with ID {request.GrantorUserId} not found.");
        }

        var emergencyAccess = new EmergencyAccess
        {
            GrantorId = grantor.Id,
            Type = request.Type,
            Status = request.Status,
            WaitTimeDays = request.WaitTimeDays,
        };

        if (request.Status == EmergencyAccessStatusType.Invited)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                throw new ArgumentException("Email is required for the Invited status.");
            }

            emergencyAccess.Email = request.Email.ToLowerInvariant();
        }
        else
        {
            if (request.GranteeUserId == null)
            {
                throw new ArgumentException($"GranteeUserId is required for the {request.Status} status.");
            }

            var grantee = await userRepository.GetByIdAsync(request.GranteeUserId.Value);
            if (grantee == null)
            {
                throw new Exception($"User with ID {request.GranteeUserId} not found.");
            }
            if (grantee.Id == grantor.Id)
            {
                throw new ArgumentException("The grantee must be a different user than the grantor.");
            }

            emergencyAccess.GranteeId = grantee.Id;

            if (request.Status >= EmergencyAccessStatusType.Confirmed)
            {
                if (string.IsNullOrWhiteSpace(request.GrantorUserKeyB64))
                {
                    throw new ArgumentException($"GrantorUserKeyB64 is required for the {request.Status} status.");
                }
                if (string.IsNullOrWhiteSpace(grantee.PublicKey))
                {
                    throw new InvalidOperationException(
                        $"Grantee {grantee.Id} has no public key; seed a user with keys to reach the {request.Status} status.");
                }

                emergencyAccess.KeyEncrypted = RustSdkService.GenerateUserOrganizationKey(
                    grantee.PublicKey, request.GrantorUserKeyB64);
            }
        }

        if (request.Status == EmergencyAccessStatusType.RecoveryInitiated)
        {
            var now = DateTime.UtcNow;
            emergencyAccess.RecoveryInitiatedDate = now;
            emergencyAccess.LastNotificationDate = now;
        }
        else if (request.Status == EmergencyAccessStatusType.RecoveryApproved)
        {
            // Recovery started early enough that the wait time has already elapsed.
            var initiated = DateTime.UtcNow.AddDays(-request.WaitTimeDays);
            emergencyAccess.RecoveryInitiatedDate = initiated;
            emergencyAccess.LastNotificationDate = initiated;
        }

        await emergencyAccessRepository.CreateAsync(emergencyAccess);

        return new SceneResult<Result>(
            result: new Result
            {
                EmergencyAccessId = emergencyAccess.Id,
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
