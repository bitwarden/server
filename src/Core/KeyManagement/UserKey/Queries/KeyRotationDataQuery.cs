using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.Utilities;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.UserKey.Models.Data;
using Bit.Core.KeyManagement.UserKey.Queries.Interfaces;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;

namespace Bit.Core.KeyManagement.UserKey.Queries;

public class KeyRotationDataQuery : IKeyRotationDataQuery
{
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IEmergencyAccessRepository _emergencyAccessRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IWebAuthnCredentialRepository _webAuthnCredentialRepository;
    private readonly IUserRepository _userRepository;

    public KeyRotationDataQuery(IOrganizationUserRepository organizationUserRepository,
        IEmergencyAccessRepository emergencyAccessRepository,
        IDeviceRepository deviceRepository,
        IWebAuthnCredentialRepository webAuthnCredentialRepository,
        IUserRepository userRepository)
    {
        _organizationUserRepository = organizationUserRepository;
        _emergencyAccessRepository = emergencyAccessRepository;
        _deviceRepository = deviceRepository;
        _webAuthnCredentialRepository = webAuthnCredentialRepository;
        _userRepository = userRepository;
    }

    public async Task<KeyRotationData> Run(User user)
    {
        var organizationTask = _organizationUserRepository.GetManyDetailsByUserAsync(user.Id);
        var emergencyAccessTask = _emergencyAccessRepository.GetManyDetailsByGrantorIdAsync(user.Id);
        var devicesTask = _deviceRepository.GetManyByUserIdAsync(user.Id);
        var passkeysTask = _webAuthnCredentialRepository.GetManyByUserIdAsync(user.Id);

        await Task.WhenAll(organizationTask, emergencyAccessTask, devicesTask, passkeysTask);

        return new KeyRotationData
        {
            OrganizationPasswordResetKeyData = MapResetPasswordOrganizations(organizationTask.Result),
            EmergencyAccessKeyData = await MapEmergencyAccessesAsync(emergencyAccessTask.Result),
            TrustedDeviceKeyData = MapTrustedDevices(devicesTask.Result),
            PasskeyKeyData = MapPasskeys(passkeysTask.Result)
        };
    }

    private static List<OrganizationPasswordResetKeyData> MapResetPasswordOrganizations(
        IEnumerable<OrganizationUserOrganizationDetails> organizationUserDetails) =>
        [.. organizationUserDetails
            .Where(o => OrganizationUser.IsValidResetPasswordKey(o.ResetPasswordKey))
            .Select(o => new OrganizationPasswordResetKeyData
            {
                OrganizationId = o.OrganizationId,
                OrganizationName = o.Name,
                OrganizationPublicKey = o.PublicKey!
            })];

    private async Task<List<EmergencyAccessKeyData>> MapEmergencyAccessesAsync(
        IEnumerable<EmergencyAccessDetails> emergencyAccesses)
    {
        var rotatable = emergencyAccesses
            .Where(ea => ea.KeyEncrypted != null)
            .ToList();

        if (rotatable.Count == 0)
        {
            return [];
        }

        var granteeIds = rotatable
            .Where(ea => ea.GranteeId.HasValue)
            .Select(ea => ea.GranteeId!.Value)
            .Distinct()
            .ToList();

        var granteePublicKeys = granteeIds.Count == 0
            ? []
            : (await _userRepository.GetManyAsync(granteeIds)).ToDictionary(u => u.Id, u => u.PublicKey);

        return
        [
            .. rotatable
                .Where(ea => ea.GranteeId.HasValue
                            && granteePublicKeys.TryGetValue(ea.GranteeId.Value, out var pk)
                            && pk != null)
                .Select(ea => new EmergencyAccessKeyData
                {
                    Id = ea.Id,
                    GranteeId = ea.GranteeId,
                    GranteeName = ea.GranteeName,
                    GranteeEmail = ea.GranteeEmail,
                    PublicKey = granteePublicKeys[ea.GranteeId!.Value]!
                })
        ];
    }

    private static List<TrustedDeviceKeyData> MapTrustedDevices(IEnumerable<Device> devices) =>
    [
        .. devices
            .Where(d => d.IsTrusted())
            .Select(d => new TrustedDeviceKeyData
            {
                Id = d.Id, EncryptedPublicKey = d.EncryptedPublicKey!, EncryptedUserKey = d.EncryptedUserKey!
            })
    ];

    private static List<PasskeyKeyData> MapPasskeys(IEnumerable<WebAuthnCredential> passkeys) =>
        [.. passkeys
            .Where(p => p.GetPrfStatus() == WebAuthnPrfStatus.Enabled)
            .Select(p => new PasskeyKeyData
            {
                Id = p.Id,
                EncryptedPublicKey = p.EncryptedPublicKey,
                EncryptedUserKey = p.EncryptedUserKey
            })];
}
