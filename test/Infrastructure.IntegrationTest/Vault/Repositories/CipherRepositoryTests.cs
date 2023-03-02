using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Repositories;

public class CipherRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task DeleteAsync_UpdatesUserRevisionDate(
        IUserRepository userRepository,
        ICipherRepository cipherRepository,
        ITestDatabaseHelper helper)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = "test@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var cipher = await cipherRepository.CreateAsync(new Cipher
        {
            Type = CipherType.Login,
            UserId = user.Id,
        });

        helper.ClearTracker();

        await cipherRepository.DeleteAsync(cipher);

        var deletedCipher = await cipherRepository.GetByIdAsync(cipher.Id);

        Assert.Null(deletedCipher);
        var updatedUser = await userRepository.GetByIdAsync(user.Id);
        Assert.NotEqual(updatedUser.AccountRevisionDate, user.AccountRevisionDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_UpdateWithCollecitons_Works(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        ICollectionRepository collectionRepository,
        ICipherRepository cipherRepository,
        ICollectionCipherRepository collectionCipherRepository,
        ITestDatabaseHelper helper)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = "test@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var organization = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Organization",
        });

        await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.Owner,
        });

        var collection = await collectionRepository.CreateAsync(new Collection
        {
            Name = "Test Collection",
            OrganizationId = organization.Id
        });

        helper.ClearTracker();

        await cipherRepository.CreateAsync(new CipherDetails
        {
            Type = CipherType.Login,
            OrganizationId = organization.Id,
        }, new List<Guid>
        {
            collection.Id,
        });

        var updatedUser = await userRepository.GetByIdAsync(user.Id);

        Assert.NotEqual(updatedUser.AccountRevisionDate, user.AccountRevisionDate);

        var collectionCiphers = await collectionCipherRepository.GetManyByOrganizationIdAsync(organization.Id);
        Assert.NotEmpty(collectionCiphers);
    }
}
