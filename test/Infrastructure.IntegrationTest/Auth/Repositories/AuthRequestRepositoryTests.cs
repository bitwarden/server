using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Auth.Repositories;

public class AuthRequestRepositoryTests
{
    private readonly static TimeSpan _userExpiration = TimeSpan.FromMinutes(15);
    private readonly static TimeSpan _adminExpiration = TimeSpan.FromDays(6);
    private readonly static TimeSpan _adminApprovalExpiration = TimeSpan.FromHours(12);

    [DatabaseTheory, DatabaseData]
    public async Task DeleteExpiredAsync_Works(
        IAuthRequestRepository authRequestRepository,
        IUserRepository userRepository,
        ITestDatabaseHelper helper)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        // A user auth request type that has passed it's expiration time, should be deleted.
        var userExpiredAuthRequest = await authRequestRepository.CreateAsync(
            CreateAuthRequest(user.Id, AuthRequestType.AuthenticateAndUnlock, CreateExpiredDate(_userExpiration)));

        // An AdminApproval request that hasn't had any action taken on it and has passed it's expiration time, should be deleted.
        var adminApprovalExpiredAuthRequest = await authRequestRepository.CreateAsync(
            CreateAuthRequest(user.Id, AuthRequestType.AdminApproval, CreateExpiredDate(_adminExpiration)));

        // An AdminApproval request that was approved before it expired but the user has been approved for too long, should be deleted.
        var adminApprovedExpiredAuthRequest = await authRequestRepository.CreateAsync(
            CreateAuthRequest(user.Id, AuthRequestType.AdminApproval, DateTime.UtcNow.AddDays(-6), true, CreateExpiredDate(_adminApprovalExpiration)));

        // An AdminApproval request that was rejected within it's allowed lifetime but has no gone past it's expiration time, should be deleted.
        var adminRejectedExpiredAuthRequest = await authRequestRepository.CreateAsync(
            CreateAuthRequest(user.Id, AuthRequestType.AdminApproval, CreateExpiredDate(_adminExpiration), false, DateTime.UtcNow.AddHours(-1)));

        // A User AuthRequest that was created just a minute ago.
        var notExpiredUserAuthRequest = await authRequestRepository.CreateAsync(
            CreateAuthRequest(user.Id, AuthRequestType.Unlock, DateTime.UtcNow.AddMinutes(-1)));

        // An AdminApproval AuthRequest that was create 6 days 23 hours 59 minutes 59 seconds ago which is right on the edge of still being valid
        var notExpiredAdminApprovalRequest = await authRequestRepository.CreateAsync(
            CreateAuthRequest(user.Id, AuthRequestType.AdminApproval, DateTime.UtcNow.Add(new TimeSpan(days: 6, hours: 23, minutes: 59, seconds: 59))));

        // An AdminApproval AuthRequest that was created a week ago but just approved 11 hours ago.
        var notExpiredApprovedAdminApprovalRequest = await authRequestRepository.CreateAsync(
            CreateAuthRequest(user.Id, AuthRequestType.AdminApproval, DateTime.UtcNow.AddDays(7), true, DateTime.UtcNow.AddHours(11)));

        helper.ClearTracker();

        var numberOfDeleted = await authRequestRepository.DeleteExpiredAsync(_userExpiration, _adminExpiration, _adminApprovalExpiration);

        // Ensure all the AuthRequests that should have been deleted, have been deleted.
        Assert.Null(await authRequestRepository.GetByIdAsync(userExpiredAuthRequest.Id));
        Assert.Null(await authRequestRepository.GetByIdAsync(adminApprovalExpiredAuthRequest.Id));
        Assert.Null(await authRequestRepository.GetByIdAsync(adminApprovedExpiredAuthRequest.Id));
        Assert.Null(await authRequestRepository.GetByIdAsync(adminRejectedExpiredAuthRequest.Id));

        // Ensure that all the AuthRequests that should have been left alone, were.
        Assert.NotNull(await authRequestRepository.GetByIdAsync(notExpiredUserAuthRequest.Id));
        Assert.NotNull(await authRequestRepository.GetByIdAsync(notExpiredAdminApprovalRequest.Id));
        Assert.NotNull(await authRequestRepository.GetByIdAsync(notExpiredApprovedAdminApprovalRequest.Id));

        // Ensure the repository responds with the amount of items it deleted and it deleted the right amount.
        // NOTE: On local development this might fail on it's first run because the developer could have expired AuthRequests
        // on their machine but aren't running the job that would delete them. The second run of this test should succeed.
        Assert.Equal(4, numberOfDeleted);
    }

    private static AuthRequest CreateAuthRequest(Guid userId, AuthRequestType authRequestType, DateTime creationDate, bool? approved = null, DateTime? responseDate = null)
    {
        return new AuthRequest
        {
            UserId = userId,
            Type = authRequestType,
            Approved = approved,
            RequestDeviceIdentifier = "something", // TODO: EF Doesn't enforce this as not null
            RequestIpAddress = "1.1.1.1", // TODO: EF Doesn't enforce this as not null
            AccessCode = "test_access_code", // TODO: EF Doesn't enforce this as not null
            PublicKey = "test_public_key", // TODO: EF Doesn't enforce this as not null
            CreationDate = creationDate,
            ResponseDate = responseDate,
        };
    }

    private static DateTime CreateExpiredDate(TimeSpan expirationPeriod)
    {
        var exp = expirationPeriod + TimeSpan.FromMinutes(1);
        return DateTime.UtcNow.Add(exp.Negate());
    }
}
