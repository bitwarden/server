using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Repositories;
using Bit.Infrastructure.IntegrationTest.Comparers;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Tools;

public class ReceiveRepositoryTests
{
    private static async Task<User> CreateTestUserAsync(IUserRepository userRepository)
    {
        var id = Guid.NewGuid();
        return await userRepository.CreateAsync(new User
        {
            Id = id,
            Name = $"test-{id}",
            Email = $"{id}@example.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });
    }

    private static Receive NewReceive(Guid userId, DateTime? expirationDate = null, string secret = "randomSecret123") => new()
    {
        UserId = userId,
        Data = "{\"File\": \"2.t|t|t\"}",
        UserKeyWrappedSharedContentEncryptionKey = "2.scek|iv|ct",
        UserKeyWrappedPrivateKey = "2.privkey|iv|ct",
        ScekWrappedPublicKey = "2.pubkey|iv|ct",
        Secret = secret,
        UploadCount = 0,
        ExpirationDate = expirationDate,
    };

    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_Works(
        IUserRepository userRepository,
        IReceiveRepository receiveRepository)
    {
        var user = await CreateTestUserAsync(userRepository);
        var expirationDate = DateTime.UtcNow.AddDays(7);

        var createdReceive = await receiveRepository.CreateAsync(
            NewReceive(user.Id, expirationDate));

        Assert.NotNull(createdReceive.ExpirationDate);
        Assert.Equal(expirationDate, createdReceive.ExpirationDate!.Value, LaxDateTimeComparer.Default);

        var receiveFromDatabase = await receiveRepository.GetByIdAsync(createdReceive.Id);
        Assert.NotNull(receiveFromDatabase);
        Assert.Equal(expirationDate, receiveFromDatabase.ExpirationDate!.Value, LaxDateTimeComparer.Default);
        Assert.Equal(0, receiveFromDatabase.UploadCount);
        Assert.Equal("2.scek|iv|ct", receiveFromDatabase.UserKeyWrappedSharedContentEncryptionKey);
        Assert.Equal("2.privkey|iv|ct", receiveFromDatabase.UserKeyWrappedPrivateKey);
        Assert.Equal("2.pubkey|iv|ct", receiveFromDatabase.ScekWrappedPublicKey);
        Assert.Equal("{\"File\": \"2.t|t|t\"}", receiveFromDatabase.Data);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByExpirationDateAsync_Works(
        IUserRepository userRepository,
        IReceiveRepository receiveRepository)
    {
        var user = await CreateTestUserAsync(userRepository);
        var expirationDate = DateTime.UtcNow.AddYears(-1);

        var shouldExpireReceive = await receiveRepository.CreateAsync(
            NewReceive(user.Id, expirationDate.AddSeconds(-2)));

        var shouldKeepReceive = await receiveRepository.CreateAsync(
            NewReceive(user.Id, expirationDate.AddSeconds(2), secret: "randomSecret456"));

        var expiredReceives = await receiveRepository.GetManyByExpirationDateAsync(expirationDate);
        Assert.Contains(expiredReceives, r => r.Id == shouldExpireReceive.Id);
        Assert.DoesNotContain(expiredReceives, r => r.Id == shouldKeepReceive.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByUserIdAsync_Works(
        IUserRepository userRepository,
        IReceiveRepository receiveRepository)
    {
        var user = await CreateTestUserAsync(userRepository);
        var otherUser = await CreateTestUserAsync(userRepository);

        var userReceive = await receiveRepository.CreateAsync(
            NewReceive(user.Id));

        var otherReceive = await receiveRepository.CreateAsync(
            NewReceive(otherUser.Id, secret: "randomSecret456"));

        var userReceives = await receiveRepository.GetManyByUserIdAsync(user.Id);
        var result = Assert.Single(userReceives);
        Assert.Equal(userReceive.Id, result.Id);
    }
}
