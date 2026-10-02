using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.UserKey.Queries;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.KeyManagement.UserKey.Queries;

[SutProviderCustomize]
public class KeyRotationDataQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task Run_ResetPasswordOrganizations_OnlyValidResetPasswordKeyIncluded(
        SutProvider<KeyRotationDataQuery> sutProvider, User user)
    {
        var valid = new OrganizationUserOrganizationDetails
        {
            OrganizationId = Guid.NewGuid(),
            Name = "Valid Org",
            PublicKey = "org-public-key",
            ResetPasswordKey = "reset-password-key"
        };
        var invalidEmpty = new OrganizationUserOrganizationDetails
        {
            OrganizationId = Guid.NewGuid(),
            Name = "Empty Key Org",
            ResetPasswordKey = "   "
        };
        var invalidNull = new OrganizationUserOrganizationDetails
        {
            OrganizationId = Guid.NewGuid(),
            Name = "Null Key Org",
            ResetPasswordKey = null
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByUserAsync(user.Id)
            .Returns(new List<OrganizationUserOrganizationDetails> { valid, invalidEmpty, invalidNull });

        var result = await sutProvider.Sut.Run(user);

        var org = Assert.Single(result.OrganizationPasswordResetKeyData);
        Assert.Equal(valid.OrganizationId, org.OrganizationId);
        Assert.Equal("Valid Org", org.OrganizationName);
        Assert.Equal("org-public-key", org.OrganizationPublicKey);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetManyDetailsByUserAsync(user.Id);
    }

    [Theory]
    [BitAutoData(EmergencyAccessStatusType.Confirmed)]
    [BitAutoData(EmergencyAccessStatusType.RecoveryInitiated)]
    [BitAutoData(EmergencyAccessStatusType.RecoveryApproved)]
    public async Task Run_EmergencyAccesses_IncludesStatusWithKey(
        EmergencyAccessStatusType status, SutProvider<KeyRotationDataQuery> sutProvider, User user)
    {
        var granteeId = Guid.NewGuid();
        var ea = new EmergencyAccessDetails
        {
            Id = Guid.NewGuid(),
            GranteeId = granteeId,
            GranteeName = "Grantee Name",
            GranteeEmail = "grantee@example.com",
            KeyEncrypted = "encrypted-key",
            Status = status
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByGrantorIdAsync(user.Id)
            .Returns(new List<EmergencyAccessDetails> { ea });
        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<User> { new() { Id = granteeId, PublicKey = "grantee-public-key" } });

        var result = await sutProvider.Sut.Run(user);

        var mapped = Assert.Single(result.EmergencyAccessKeyData);
        Assert.Equal(ea.Id, mapped.Id);
        Assert.Equal(granteeId, mapped.GranteeId);
        Assert.Equal("Grantee Name", mapped.GranteeName);
        Assert.Equal("grantee-public-key", mapped.PublicKey);
    }

    [Theory]
    [BitAutoData]
    public async Task Run_EmergencyAccesses_ExcludesNullKey(
        SutProvider<KeyRotationDataQuery> sutProvider, User user)
    {
        var ea = new EmergencyAccessDetails
        {
            Id = Guid.NewGuid(),
            GranteeId = Guid.NewGuid(),
            KeyEncrypted = null,
            Status = EmergencyAccessStatusType.Invited
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByGrantorIdAsync(user.Id)
            .Returns(new List<EmergencyAccessDetails> { ea });

        var result = await sutProvider.Sut.Run(user);

        Assert.Empty(result.EmergencyAccessKeyData);
    }

    [Theory]
    [BitAutoData]
    public async Task Run_EmergencyAccesses_MapsNameAndEmailSeparately(
        SutProvider<KeyRotationDataQuery> sutProvider, User user)
    {
        var granteeId = Guid.NewGuid();
        var ea = new EmergencyAccessDetails
        {
            Id = Guid.NewGuid(),
            GranteeId = granteeId,
            GranteeName = null,
            GranteeEmail = "grantee@example.com",
            KeyEncrypted = "encrypted-key",
            Status = EmergencyAccessStatusType.Confirmed
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByGrantorIdAsync(user.Id)
            .Returns(new List<EmergencyAccessDetails> { ea });
        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<User> { new() { Id = granteeId, PublicKey = "grantee-public-key" } });

        var result = await sutProvider.Sut.Run(user);

        var mapped = Assert.Single(result.EmergencyAccessKeyData);
        Assert.Null(mapped.GranteeName);
        Assert.Equal("grantee@example.com", mapped.GranteeEmail);
    }

    [Theory]
    [BitAutoData]
    public async Task Run_EmergencyAccesses_MissingUserPublicKeyExcluded(
        SutProvider<KeyRotationDataQuery> sutProvider, User user)
    {
        var ea = new EmergencyAccessDetails
        {
            Id = Guid.NewGuid(),
            GranteeId = Guid.NewGuid(),
            GranteeEmail = "grantee@example.com",
            KeyEncrypted = "encrypted-key",
            Status = EmergencyAccessStatusType.Confirmed
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByGrantorIdAsync(user.Id)
            .Returns(new List<EmergencyAccessDetails> { ea });
        // GetManyAsync returns no users for the grantee id.
        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<User>());

        var result = await sutProvider.Sut.Run(user);

        // PublicKey is required, so an emergency access without a resolvable public key is excluded.
        Assert.Empty(result.EmergencyAccessKeyData);
    }

    [Theory]
    [BitAutoData]
    public async Task Run_EmergencyAccesses_NullGranteeIdExcluded(
        SutProvider<KeyRotationDataQuery> sutProvider, User user)
    {
        var ea = new EmergencyAccessDetails
        {
            Id = Guid.NewGuid(),
            GranteeId = null,
            GranteeEmail = "grantee@example.com",
            KeyEncrypted = "encrypted-key",
            Status = EmergencyAccessStatusType.Confirmed
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByGrantorIdAsync(user.Id)
            .Returns([ea]);

        var result = await sutProvider.Sut.Run(user);

        // Without a grantee id there is no public key to resolve, so the emergency access is excluded.
        Assert.Empty(result.EmergencyAccessKeyData);
        // No grantee ids to look up, so the bulk fetch should be skipped.
        await sutProvider.GetDependency<IUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>());
    }

    [Theory]
    [BitAutoData]
    public async Task Run_EmergencyAccesses_BulkFetchesDistinctGranteeIdsOnce(
        SutProvider<KeyRotationDataQuery> sutProvider, User user)
    {
        var granteeId1 = Guid.NewGuid();
        var granteeId2 = Guid.NewGuid();
        var emergencyAccesses = new List<EmergencyAccessDetails>
        {
            new()
            {
                Id = Guid.NewGuid(),
                GranteeId = granteeId1,
                KeyEncrypted = "key1",
                Status = EmergencyAccessStatusType.Confirmed
            },
            new()
            {
                Id = Guid.NewGuid(),
                GranteeId = granteeId1,
                KeyEncrypted = "key2",
                Status = EmergencyAccessStatusType.RecoveryApproved
            },
            new()
            {
                Id = Guid.NewGuid(),
                GranteeId = granteeId2,
                KeyEncrypted = "key3",
                Status = EmergencyAccessStatusType.Confirmed
            }
        };

        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByGrantorIdAsync(user.Id)
            .Returns(emergencyAccesses);
        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(
            [
                new User { Id = granteeId1, PublicKey = "key1-pub" },
                new User { Id = granteeId2, PublicKey = "key2-pub" }
            ]);

        var result = await sutProvider.Sut.Run(user);

        Assert.Equal(3, result.EmergencyAccessKeyData.Count());
        await sutProvider.GetDependency<IUserRepository>()
            .Received(1)
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Count() == 2 && ids.Contains(granteeId1) && ids.Contains(granteeId2)));
    }

    [Theory]
    [BitAutoData]
    public async Task Run_TrustedDevices_OnlyTrustedIncluded(
        SutProvider<KeyRotationDataQuery> sutProvider, User user)
    {
        var trusted = new Device
        {
            Id = Guid.NewGuid(),
            EncryptedUserKey = "encrypted-user-key",
            EncryptedPublicKey = "encrypted-public-key",
            EncryptedPrivateKey = "encrypted-private-key"
        };
        var untrusted = new Device
        {
            Id = Guid.NewGuid(),
            EncryptedUserKey = null,
            EncryptedPublicKey = null,
            EncryptedPrivateKey = null
        };

        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns([trusted, untrusted]);

        var result = await sutProvider.Sut.Run(user);

        var device = Assert.Single(result.TrustedDeviceKeyData);
        Assert.Equal(trusted.Id, device.Id);
        Assert.Equal("encrypted-public-key", device.EncryptedPublicKey);
        Assert.Equal("encrypted-user-key", device.EncryptedUserKey);
    }

    [Theory]
    [BitAutoData]
    public async Task Run_Passkeys_OnlyPrfEnabledIncluded(
        SutProvider<KeyRotationDataQuery> sutProvider, User user)
    {
        var enabled = new WebAuthnCredential
        {
            Id = Guid.NewGuid(),
            SupportsPrf = true,
            EncryptedUserKey = "encrypted-user-key",
            EncryptedPublicKey = "encrypted-public-key",
            EncryptedPrivateKey = "encrypted-private-key"
        };
        var notEnabled = new WebAuthnCredential { Id = Guid.NewGuid(), SupportsPrf = false };

        sutProvider.GetDependency<IWebAuthnCredentialRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns([enabled, notEnabled]);

        var result = await sutProvider.Sut.Run(user);

        var passkey = Assert.Single(result.PasskeyKeyData);
        Assert.Equal(enabled.Id, passkey.Id);
        Assert.Equal("encrypted-public-key", passkey.EncryptedPublicKey);
        Assert.Equal("encrypted-user-key", passkey.EncryptedUserKey);
    }

    [Theory]
    [BitAutoData]
    public async Task Run_AllEmptyInputs_ReturnsNonNullEmptyCollections(
        SutProvider<KeyRotationDataQuery> sutProvider, User user)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByUserAsync(user.Id)
            .Returns([]);
        sutProvider.GetDependency<IEmergencyAccessRepository>()
            .GetManyDetailsByGrantorIdAsync(user.Id)
            .Returns([]);
        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns([]);
        sutProvider.GetDependency<IWebAuthnCredentialRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns([]);

        var result = await sutProvider.Sut.Run(user);

        Assert.NotNull(result.OrganizationPasswordResetKeyData);
        Assert.Empty(result.OrganizationPasswordResetKeyData);
        Assert.NotNull(result.EmergencyAccessKeyData);
        Assert.Empty(result.EmergencyAccessKeyData);
        Assert.NotNull(result.TrustedDeviceKeyData);
        Assert.Empty(result.TrustedDeviceKeyData);
        Assert.NotNull(result.PasskeyKeyData);
        Assert.Empty(result.PasskeyKeyData);
    }
}
