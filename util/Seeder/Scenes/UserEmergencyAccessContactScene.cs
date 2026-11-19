using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Extensions;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

/// <summary>
/// Seeds an emergency access contact for an existing user (the grantor), in the requested
/// <see cref="EmergencyAccessStatusType"/>.
/// </summary>
public class UserEmergencyAccessContactScene(
    IUserRepository userRepository,
    IEmergencyAccessRepository emergencyAccessRepository,
    IManglerService manglerService) : IScene<UserEmergencyAccessContactScene.Request, UserEmergencyAccessContactScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid GrantorUserId { get; set; }
        [Required]
        public required EmergencyAccessStatusType Status { get; set; }
        [EmailAddress]
        [MaxLength(256)]
        public string? Email { get; set; }
        public Guid? GranteeUserId { get; set; }
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
        var grantor = await userRepository.GetUserOrThrowAsync(request.GrantorUserId);

        var emergencyAccess = new EmergencyAccess
        {
            GrantorId = grantor.Id,
            Type = request.Type,
            Status = request.Status,
            WaitTimeDays = request.WaitTimeDays,
        };

        if (request.Status == EmergencyAccessStatusType.Invited)
        {
            emergencyAccess.Email = request.Email?.ToLowerInvariant()
                ?? throw new ArgumentException("Email is required for the Invited status.");
        }
        else
        {
            var granteeUserId = request.GranteeUserId
                ?? throw new ArgumentException($"GranteeUserId is required for the {request.Status} status.");
            var grantee = await userRepository.GetUserOrThrowAsync(granteeUserId);
            emergencyAccess.GranteeId = grantee.Id;

            if (request.Status >= EmergencyAccessStatusType.Confirmed)
            {
                emergencyAccess.KeyEncrypted = RustSdkService.GenerateUserOrganizationKey(
                    grantee.PublicKey!, request.GrantorUserKeyB64!);
            }
        }

        var recoveryInitiated = request.Status switch
        {
            EmergencyAccessStatusType.RecoveryInitiated => DateTime.UtcNow,
            // Recovery started early enough that the wait time has already elapsed.
            EmergencyAccessStatusType.RecoveryApproved => DateTime.UtcNow.AddDays(-request.WaitTimeDays),
            _ => (DateTime?)null,
        };
        emergencyAccess.RecoveryInitiatedDate = recoveryInitiated;
        emergencyAccess.LastNotificationDate = recoveryInitiated;

        await emergencyAccessRepository.CreateAsync(emergencyAccess);

        return new SceneResult<Result>(
            result: new Result
            {
                EmergencyAccessId = emergencyAccess.Id,
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
