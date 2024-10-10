using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Repositories;

public class OrganizationUserRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task DeleteAsync_Works(IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = user.Email, // TODO: EF does not enfore this being NOT NULL
            Plan = "Test", // TODO: EF does not enforce this being NOT NULl
        });

        var orgUser = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        await organizationUserRepository.DeleteAsync(orgUser);

        var newUser = await userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(newUser);
        Assert.NotEqual(newUser.AccountRevisionDate, user.AccountRevisionDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task DeleteManyAsync_Works(IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var user1 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 1",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var user2 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 2",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = user1.Email, // TODO: EF does not enforce this being NOT NULl
            Plan = "Test", // TODO: EF does not enforce this being NOT NULl
        });

        var orgUser1 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user1.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        var orgUser2 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user2.Id,
            Status = OrganizationUserStatusType.Confirmed,
        });

        await organizationUserRepository.DeleteManyAsync(new List<Guid>
        {
            orgUser1.Id,
            orgUser2.Id,
        });

        var updatedUser1 = await userRepository.GetByIdAsync(user1.Id);
        Assert.NotNull(updatedUser1);
        var updatedUser2 = await userRepository.GetByIdAsync(user2.Id);
        Assert.NotNull(updatedUser2);

        Assert.NotEqual(updatedUser1.AccountRevisionDate, user1.AccountRevisionDate);
        Assert.NotEqual(updatedUser2.AccountRevisionDate, user2.AccountRevisionDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyAccountRecoveryDetailsByOrganizationUserAsync_Works(IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var user1 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 1",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 1,
            KdfMemory = 2,
            KdfParallelism = 3
        });

        var user2 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 2",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.Argon2id,
            KdfIterations = 4,
            KdfMemory = 5,
            KdfParallelism = 6
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = user1.Email, // TODO: EF does not enforce this being NOT NULl
            Plan = "Test", // TODO: EF does not enforce this being NOT NULl
            PrivateKey = "privatekey",
        });

        var orgUser1 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user1.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey1",
        });

        var orgUser2 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user2.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey2",
        });

        var recoveryDetails = await organizationUserRepository.GetManyAccountRecoveryDetailsByOrganizationUserAsync(
            organization.Id,
            new[]
            {
                orgUser1.Id,
                orgUser2.Id,
            });

        Assert.NotNull(recoveryDetails);
        Assert.Equal(2, recoveryDetails.Count());
        Assert.Contains(recoveryDetails, r =>
            r.OrganizationUserId == orgUser1.Id &&
            r.Kdf == KdfType.PBKDF2_SHA256 &&
            r.KdfIterations == 1 &&
            r.KdfMemory == 2 &&
            r.KdfParallelism == 3 &&
            r.ResetPasswordKey == "resetpasswordkey1" &&
            r.EncryptedPrivateKey == "privatekey");
        Assert.Contains(recoveryDetails, r =>
            r.OrganizationUserId == orgUser2.Id &&
            r.Kdf == KdfType.Argon2id &&
            r.KdfIterations == 4 &&
            r.KdfMemory == 5 &&
            r.KdfParallelism == 6 &&
            r.ResetPasswordKey == "resetpasswordkey2" &&
            r.EncryptedPrivateKey == "privatekey");
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyDetailsByUserAsync_Works(IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var user1 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 1",
            Email = $"test+{Guid.NewGuid()}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 1,
            KdfMemory = 2,
            KdfParallelism = 3
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = user1.Email, // TODO: EF does not enforce this being NOT NULl
            Plan = "Test", // TODO: EF does not enforce this being NOT NULl
            PrivateKey = "privatekey",
        });

        var orgUser1 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user1.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey1",
        });

        var responseModel = await organizationUserRepository.GetManyDetailsByUserAsync(user1.Id);

        Assert.NotNull(responseModel);
        Assert.Single(responseModel);
        var result = responseModel.Single();
        Assert.Equal(organization.Id, result.OrganizationId);
        Assert.Equal(user1.Id, result.UserId);
        Assert.Equal(orgUser1.Id, result.OrganizationUserId);
        Assert.Equal(organization.Name, result.Name);
        Assert.Equal(organization.UsePolicies, result.UsePolicies);
        Assert.Equal(organization.UseSso, result.UseSso);
        Assert.Equal(organization.UseKeyConnector, result.UseKeyConnector);
        Assert.Equal(organization.UseScim, result.UseScim);
        Assert.Equal(organization.UseGroups, result.UseGroups);
        Assert.Equal(organization.UseDirectory, result.UseDirectory);
        Assert.Equal(organization.UseEvents, result.UseEvents);
        Assert.Equal(organization.UseTotp, result.UseTotp);
        Assert.Equal(organization.Use2fa, result.Use2fa);
        Assert.Equal(organization.UseApi, result.UseApi);
        Assert.Equal(organization.UseResetPassword, result.UseResetPassword);
        Assert.Equal(organization.UseSecretsManager, result.UseSecretsManager);
        Assert.Equal(organization.UsePasswordManager, result.UsePasswordManager);
        Assert.Equal(organization.UsersGetPremium, result.UsersGetPremium);
        Assert.Equal(organization.UseCustomPermissions, result.UseCustomPermissions);
        Assert.Equal(organization.SelfHost, result.SelfHost);
        Assert.Equal(organization.Seats, result.Seats);
        Assert.Equal(organization.MaxCollections, result.MaxCollections);
        Assert.Equal(organization.MaxStorageGb, result.MaxStorageGb);
        Assert.Equal(organization.Identifier, result.Identifier);
        Assert.Equal(orgUser1.Key, result.Key);
        Assert.Equal(orgUser1.ResetPasswordKey, result.ResetPasswordKey);
        Assert.Equal(organization.PublicKey, result.PublicKey);
        Assert.Equal(organization.PrivateKey, result.PrivateKey);
        Assert.Equal(orgUser1.Status, result.Status);
        Assert.Equal(orgUser1.Type, result.Type);
        Assert.Equal(organization.Enabled, result.Enabled);
        Assert.Equal(organization.PlanType, result.PlanType);
        Assert.Equal(orgUser1.Permissions, result.Permissions);
        Assert.Equal(organization.SmSeats, result.SmSeats);
        Assert.Equal(organization.SmServiceAccounts, result.SmServiceAccounts);
        Assert.Equal(organization.LimitCollectionCreation, result.LimitCollectionCreation);
        Assert.Equal(organization.LimitCollectionDeletion, result.LimitCollectionDeletion);
        // Deprecated: https://bitwarden.atlassian.net/browse/PM-10863
        Assert.Equal(organization.LimitCollectionCreationDeletion, result.LimitCollectionCreationDeletion);
        Assert.Equal(organization.AllowAdminAccessToAllCollectionItems, result.AllowAdminAccessToAllCollectionItems);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationWithClaimedDomainsAsync_WithVerifiedDomain_WithOneMatchingEmailDomain_ReturnsSingle(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationDomainRepository organizationDomainRepository)
    {
        var id = Guid.NewGuid();
        var domainName = $"{id}.example.com";

        var user1 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 1",
            Email = $"test+{id}@{domainName}",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 1,
            KdfMemory = 2,
            KdfParallelism = 3
        });

        var user2 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 2",
            Email = $"test+{id}@x-{domainName}", // Different domain
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 1,
            KdfMemory = 2,
            KdfParallelism = 3
        });

        var user3 = await userRepository.CreateAsync(new User
        {
            Name = "Test User 2",
            Email = $"test+{id}@{domainName}.example.com", // Different domain
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 1,
            KdfMemory = 2,
            KdfParallelism = 3
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = $"Test Org {id}",
            BillingEmail = user1.Email, // TODO: EF does not enforce this being NOT NULl
            Plan = "Test", // TODO: EF does not enforce this being NOT NULl
            PrivateKey = "privatekey",
        });

        var organizationDomain = new OrganizationDomain
        {
            OrganizationId = organization.Id,
            DomainName = domainName,
            Txt = "btw+12345",
        };
        organizationDomain.SetVerifiedDate();
        organizationDomain.SetNextRunDate(12);
        organizationDomain.SetJobRunCount();
        await organizationDomainRepository.CreateAsync(organizationDomain);

        var orgUser1 = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user1.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey1",
        });

        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user2.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey1",
        });

        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = user3.Id,
            Status = OrganizationUserStatusType.Confirmed,
            ResetPasswordKey = "resetpasswordkey1",
        });

        var responseModel = await organizationUserRepository.GetManyByOrganizationWithClaimedDomainsAsync(organization.Id);

        Assert.NotNull(responseModel);
        Assert.Single(responseModel);
        Assert.Equal(orgUser1.Id, responseModel.Single().Id);
    }
}
