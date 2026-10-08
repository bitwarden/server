using Bit.AgentFill.Entities;
using Bit.AgentFill.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AgentFill;

public class AgentFillApprovalRequestRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_ThenGetByIdAsync_RoundTripsForTheOwnerOnly(
        IServiceProvider serviceProvider, Database database,
        IUserRepository userRepository, IDeviceRepository deviceRepository)
    {
        var sut = CreateRepository(serviceProvider, database);
        var (user, device) = await CreateUserAndDeviceAsync(userRepository, deviceRepository);
        var otherUser = await userRepository.CreateTestUserAsync("other");
        var request = NewRequest(user.Id, device.Id, DateTime.UtcNow);

        await sut.CreateAsync(request);

        var stored = await sut.GetByIdAsync(request.Id, user.Id);
        Assert.NotNull(stored);
        Assert.Equal(request.UserId, stored.UserId);
        Assert.Equal(request.RequestDeviceId, stored.RequestDeviceId);
        Assert.Equal(request.SealedRequest, stored.SealedRequest);
        Assert.Null(stored.SealedResponse);
        Assert.Null(stored.ResponseDeviceId);
        Assert.Null(stored.ResponseDate);
        // Dapper sends DateTime parameters as SQL Server datetime, which rounds to 1/300 of a second.
        Assert.Equal(request.CreationDate, stored.CreationDate, TimeSpan.FromMilliseconds(5));
        Assert.Equal(request.ExpirationDate, stored.ExpirationDate, TimeSpan.FromMilliseconds(5));

        Assert.Null(await sut.GetByIdAsync(request.Id, otherUser.Id));
    }

    [DatabaseTheory, DatabaseData]
    public async Task AnswerAsync_FirstAnswerWins(
        IServiceProvider serviceProvider, Database database,
        IUserRepository userRepository, IDeviceRepository deviceRepository)
    {
        var sut = CreateRepository(serviceProvider, database);
        var (user, device) = await CreateUserAndDeviceAsync(userRepository, deviceRepository);
        var request = NewRequest(user.Id, device.Id, DateTime.UtcNow);
        await sut.CreateAsync(request);

        var now = DateTime.UtcNow;
        var first = await sut.AnswerAsync(request.Id, user.Id, "first-response", device.Id, now);
        var second = await sut.AnswerAsync(request.Id, user.Id, "second-response", device.Id, now);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        var stored = await sut.GetByIdAsync(request.Id, user.Id);
        Assert.Equal("first-response", stored!.SealedResponse);
        Assert.Equal(device.Id, stored.ResponseDeviceId);
        Assert.NotNull(stored.ResponseDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task AnswerAsync_Concurrent_StoresExactlyOneResponse(
        IServiceProvider serviceProvider, Database database,
        IUserRepository userRepository, IDeviceRepository deviceRepository)
    {
        var sut = CreateRepository(serviceProvider, database);
        var (user, device) = await CreateUserAndDeviceAsync(userRepository, deviceRepository);
        var request = NewRequest(user.Id, device.Id, DateTime.UtcNow);
        await sut.CreateAsync(request);

        var now = DateTime.UtcNow;
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(i =>
            Task.Run(() => sut.AnswerAsync(request.Id, user.Id, $"response-{i}", device.Id, now))));

        Assert.Equal(1, results.Sum());
        var winner = Array.IndexOf(results, 1);
        var stored = await sut.GetByIdAsync(request.Id, user.Id);
        Assert.Equal($"response-{winner}", stored!.SealedResponse);
    }

    [DatabaseTheory, DatabaseData]
    public async Task AnswerAsync_AfterExpiration_DoesNotUpdate(
        IServiceProvider serviceProvider, Database database,
        IUserRepository userRepository, IDeviceRepository deviceRepository)
    {
        var sut = CreateRepository(serviceProvider, database);
        var (user, device) = await CreateUserAndDeviceAsync(userRepository, deviceRepository);
        var request = NewRequest(user.Id, device.Id, DateTime.UtcNow.AddMinutes(-10));
        await sut.CreateAsync(request);

        var updated = await sut.AnswerAsync(request.Id, user.Id, "late-response", device.Id, DateTime.UtcNow);

        Assert.Equal(0, updated);
        Assert.Null((await sut.GetByIdAsync(request.Id, user.Id))!.SealedResponse);
    }

    [DatabaseTheory, DatabaseData]
    public async Task AnswerAsync_AnotherUser_DoesNotUpdate(
        IServiceProvider serviceProvider, Database database,
        IUserRepository userRepository, IDeviceRepository deviceRepository)
    {
        var sut = CreateRepository(serviceProvider, database);
        var (user, device) = await CreateUserAndDeviceAsync(userRepository, deviceRepository);
        var otherUser = await userRepository.CreateTestUserAsync("other");
        var request = NewRequest(user.Id, device.Id, DateTime.UtcNow);
        await sut.CreateAsync(request);

        var updated = await sut.AnswerAsync(request.Id, otherUser.Id, "forged-response", device.Id, DateTime.UtcNow);

        Assert.Equal(0, updated);
        Assert.Null((await sut.GetByIdAsync(request.Id, user.Id))!.SealedResponse);
    }

    [DatabaseTheory, DatabaseData]
    public async Task DeleteExpiredAsync_DeletesOnlyRequestsExpiredBeforeTheCutoff(
        IServiceProvider serviceProvider, Database database,
        IUserRepository userRepository, IDeviceRepository deviceRepository)
    {
        var sut = CreateRepository(serviceProvider, database);
        var (user, device) = await CreateUserAndDeviceAsync(userRepository, deviceRepository);
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-1);

        // Expired more than a day ago: deleted.
        var old = NewRequest(user.Id, device.Id, now.AddDays(-1).AddMinutes(-10));
        // Expired less than a day ago, and still pending: kept.
        var recent = NewRequest(user.Id, device.Id, now.AddHours(-1));
        var pending = NewRequest(user.Id, device.Id, now);
        await sut.CreateAsync(old);
        await sut.CreateAsync(recent);
        await sut.CreateAsync(pending);

        var deleted = await sut.DeleteExpiredAsync(cutoff);

        // Other tests may leave old rows behind, so only a lower bound holds.
        Assert.True(deleted >= 1);
        Assert.Null(await sut.GetByIdAsync(old.Id, user.Id));
        Assert.NotNull(await sut.GetByIdAsync(recent.Id, user.Id));
        Assert.NotNull(await sut.GetByIdAsync(pending.Id, user.Id));
    }

    [DatabaseTheory, DatabaseData]
    public async Task DeletingTheUser_DeletesTheirRequests(
        IServiceProvider serviceProvider, Database database,
        IUserRepository userRepository, IDeviceRepository deviceRepository)
    {
        var sut = CreateRepository(serviceProvider, database);
        var (user, device) = await CreateUserAndDeviceAsync(userRepository, deviceRepository);
        var request = NewRequest(user.Id, device.Id, DateTime.UtcNow);
        await sut.CreateAsync(request);

        // User deletion removes the user's devices first, so neither foreign key may block it.
        await userRepository.DeleteAsync(user);

        Assert.Null(await sut.GetByIdAsync(request.Id, user.Id));
    }

    /// <summary>
    /// Builds the implementation the library would pick for this database: Dapper on SQL Server, EF otherwise.
    /// </summary>
    private static IAgentFillApprovalRequestRepository CreateRepository(IServiceProvider serviceProvider,
        Database database)
        => database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf
            ? ActivatorUtilities.CreateInstance<DapperAgentFillApprovalRequestRepository>(serviceProvider)
            : ActivatorUtilities.CreateInstance<EntityFrameworkAgentFillApprovalRequestRepository>(serviceProvider);

    private static async Task<(User User, Device Device)> CreateUserAndDeviceAsync(
        IUserRepository userRepository, IDeviceRepository deviceRepository)
    {
        var user = await userRepository.CreateTestUserAsync("agent-fill");
        var device = await deviceRepository.CreateAsync(new Device
        {
            Name = "Desktop",
            UserId = user.Id,
            Type = DeviceType.MacOsDesktop,
            Identifier = Guid.NewGuid().ToString(),
        });
        return (user, device);
    }

    private static AgentFillApprovalRequest NewRequest(Guid userId, Guid deviceId, DateTime creationDate) => new()
    {
        Id = CombGuid.Generate(),
        UserId = userId,
        RequestDeviceId = deviceId,
        SealedRequest = "{\"format_version\":1,\"wrapped_cek\":\"2.x|y|z\",\"envelope\":\"sealed\"}",
        CreationDate = creationDate,
        ExpirationDate = creationDate.Add(AgentFillApprovalRequest.Lifetime),
    };
}
