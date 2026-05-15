using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.KeyManagement.Repositories;

public class UserAsymmetricKeysRepositoryTests
{
    [Theory, DatabaseData]
    public async Task RegenerateUserAsymmetricKeysAsync_UpdatesKeysOnUser(
        IUserRepository userRepository,
        IUserAsymmetricKeysRepository userAsymmetricKeysRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        user.AccountRevisionDate = DateTime.UtcNow.AddDays(-1);
        await userRepository.ReplaceAsync(user);

        var newKeys = new UserAsymmetricKeys
        {
            UserId = user.Id,
            PublicKey = "new-public-key",
            UserKeyEncryptedPrivateKey = "new-encrypted-private-key",
        };

        await userAsymmetricKeysRepository.RegenerateUserAsymmetricKeysAsync(newKeys, []);

        var updatedUser = await userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal("new-public-key", updatedUser.PublicKey);
        Assert.Equal("new-encrypted-private-key", updatedUser.PrivateKey);
        Assert.Equal(DateTime.UtcNow, updatedUser.AccountRevisionDate, TimeSpan.FromMinutes(1));
    }

    [Theory, DatabaseData]
    public async Task RegenerateUserAsymmetricKeysAsync_WithEmergencyAccessDelegate_SetsStatusToAccepted(
        IUserRepository userRepository,
        IUserAsymmetricKeysRepository userAsymmetricKeysRepository,
        IEmergencyAccessRepository emergencyAccessRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var grantorUser = await userRepository.CreateTestUserAsync("grantor");

        var ea = await emergencyAccessRepository.CreateAsync(new EmergencyAccess
        {
            GrantorId = grantorUser.Id,
            GranteeId = user.Id,
            KeyEncrypted = "old-encrypted-key",
            Status = EmergencyAccessStatusType.Confirmed,
            Type = EmergencyAccessType.View,
            WaitTimeDays = 10,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        });

        var newKeys = new UserAsymmetricKeys
        {
            UserId = user.Id,
            PublicKey = "new-public-key",
            UserKeyEncryptedPrivateKey = "new-encrypted-private-key",
        };

        var updateActions = new[]
        {
            emergencyAccessRepository.SetStatusToAcceptedForPublicKeyPairRegeneration([ea])
        };

        await userAsymmetricKeysRepository.RegenerateUserAsymmetricKeysAsync(newKeys, updateActions);

        var updatedEa = await emergencyAccessRepository.GetByIdAsync(ea.Id);
        Assert.NotNull(updatedEa);
        Assert.Equal(EmergencyAccessStatusType.Accepted, updatedEa.Status);
        Assert.Null(updatedEa.KeyEncrypted);
    }

    [Theory, DatabaseData]
    public async Task RegenerateUserAsymmetricKeysAsync_WithOrgUserStatusDelegate_SetsStatusToAccepted(
        IUserRepository userRepository,
        IUserAsymmetricKeysRepository userAsymmetricKeysRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var org = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(org, user);
        orgUser.Key = "old-org-key";
        await organizationUserRepository.ReplaceAsync(orgUser);

        var newKeys = new UserAsymmetricKeys
        {
            UserId = user.Id,
            PublicKey = "new-public-key",
            UserKeyEncryptedPrivateKey = "new-encrypted-private-key",
        };

        var updateActions = new[]
        {
            organizationUserRepository.SetStatusToAcceptedForPublicKeyPairRegeneration([orgUser])
        };

        await userAsymmetricKeysRepository.RegenerateUserAsymmetricKeysAsync(newKeys, updateActions);

        var updatedOrgUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.NotNull(updatedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Accepted, updatedOrgUser.Status);
        Assert.Null(updatedOrgUser.Key);
    }

    [Theory, DatabaseData]
    public async Task RegenerateUserAsymmetricKeysAsync_WithRemoveDelegate_DeletesOrgUser(
        IUserRepository userRepository,
        IUserAsymmetricKeysRepository userAsymmetricKeysRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        var org = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser = await organizationUserRepository.CreateRevokedTestOrganizationUserAsync(org, user);

        var newKeys = new UserAsymmetricKeys
        {
            UserId = user.Id,
            PublicKey = "new-public-key",
            UserKeyEncryptedPrivateKey = "new-encrypted-private-key",
        };

        var updateActions = new[]
        {
            organizationUserRepository.RemoveForPublicKeyPairRegeneration([orgUser])
        };

        await userAsymmetricKeysRepository.RegenerateUserAsymmetricKeysAsync(newKeys, updateActions);

        var deletedOrgUser = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.Null(deletedOrgUser);
    }

    [Theory, DatabaseData]
    public async Task RegenerateUserAsymmetricKeysAsync_WithMultipleDelegates_AllChangesApplied(
        IUserRepository userRepository,
        IUserAsymmetricKeysRepository userAsymmetricKeysRepository,
        IEmergencyAccessRepository emergencyAccessRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        var user = await userRepository.CreateTestUserAsync();
        user.AccountRevisionDate = DateTime.UtcNow.AddDays(-1);
        await userRepository.ReplaceAsync(user);
        var grantorUser = await userRepository.CreateTestUserAsync("grantor");

        var ea = await emergencyAccessRepository.CreateAsync(new EmergencyAccess
        {
            GrantorId = grantorUser.Id,
            GranteeId = user.Id,
            KeyEncrypted = "old-encrypted-key",
            Status = EmergencyAccessStatusType.RecoveryInitiated,
            Type = EmergencyAccessType.View,
            WaitTimeDays = 10,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        });

        var org1 = await organizationRepository.CreateTestOrganizationAsync();
        var confirmedOrgUser = await organizationUserRepository.CreateTestOrganizationUserAsync(org1, user);
        confirmedOrgUser.Key = "old-org-key";
        await organizationUserRepository.ReplaceAsync(confirmedOrgUser);

        var org2 = await organizationRepository.CreateTestOrganizationAsync();
        var revokedOrgUser = await organizationUserRepository.CreateRevokedTestOrganizationUserAsync(org2, user);

        var newKeys = new UserAsymmetricKeys
        {
            UserId = user.Id,
            PublicKey = "new-public-key",
            UserKeyEncryptedPrivateKey = "new-encrypted-private-key",
        };

        var updateActions = new[]
        {
            emergencyAccessRepository.SetStatusToAcceptedForPublicKeyPairRegeneration([ea]),
            organizationUserRepository.SetStatusToAcceptedForPublicKeyPairRegeneration([confirmedOrgUser]),
            organizationUserRepository.RemoveForPublicKeyPairRegeneration([revokedOrgUser]),
        };

        await userAsymmetricKeysRepository.RegenerateUserAsymmetricKeysAsync(newKeys, updateActions);

        var updatedUser = await userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal("new-public-key", updatedUser.PublicKey);
        Assert.Equal("new-encrypted-private-key", updatedUser.PrivateKey);
        Assert.Equal(DateTime.UtcNow, updatedUser.AccountRevisionDate, TimeSpan.FromMinutes(1));

        var updatedEa = await emergencyAccessRepository.GetByIdAsync(ea.Id);
        Assert.NotNull(updatedEa);
        Assert.Equal(EmergencyAccessStatusType.Accepted, updatedEa.Status);
        Assert.Null(updatedEa.KeyEncrypted);

        var updatedConfirmedOrgUser = await organizationUserRepository.GetByIdAsync(confirmedOrgUser.Id);
        Assert.NotNull(updatedConfirmedOrgUser);
        Assert.Equal(OrganizationUserStatusType.Accepted, updatedConfirmedOrgUser.Status);
        Assert.Null(updatedConfirmedOrgUser.Key);

        var deletedOrgUser = await organizationUserRepository.GetByIdAsync(revokedOrgUser.Id);
        Assert.Null(deletedOrgUser);
    }
}
