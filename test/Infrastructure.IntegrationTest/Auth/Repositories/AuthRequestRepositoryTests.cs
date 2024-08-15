using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Auth.Repositories;

public class AuthRequestRepositoryTests
{
    private readonly static TimeSpan _userRequestExpiration = TimeSpan.FromMinutes(15);
    private readonly static TimeSpan _adminRequestExpiration = TimeSpan.FromDays(6);
    private readonly static TimeSpan _afterAdminApprovalExpiration = TimeSpan.FromHours(12);

    [DatabaseTheory, DatabaseData]
    public async Task DeleteExpiredAsync_Works(
        IAuthRequestRepository authRequestRepository,
        IUserRepository userRepository)
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
            CreateAuthRequest(user.Id, AuthRequestType.AuthenticateAndUnlock, CreateExpiredDate(_userRequestExpiration)));

        // An AdminApproval request that hasn't had any action taken on it and has passed it's expiration time, should be deleted.
        var adminApprovalExpiredAuthRequest = await authRequestRepository.CreateAsync(
            CreateAuthRequest(user.Id, AuthRequestType.AdminApproval, CreateExpiredDate(_adminRequestExpiration)));

        // An AdminApproval request that was approved before it expired but the user has been approved for too long, should be deleted.
        var adminApprovedExpiredAuthRequest = await authRequestRepository.CreateAsync(
            CreateAuthRequest(user.Id, AuthRequestType.AdminApproval, DateTime.UtcNow.AddDays(-6), true, CreateExpiredDate(_afterAdminApprovalExpiration)));

        // An AdminApproval request that was rejected within it's allowed lifetime but has no gone past it's expiration time, should be deleted.
        var adminRejectedExpiredAuthRequest = await authRequestRepository.CreateAsync(
            CreateAuthRequest(user.Id, AuthRequestType.AdminApproval, CreateExpiredDate(_adminRequestExpiration), false, DateTime.UtcNow.AddHours(-1)));

        // A User AuthRequest that was created just a minute ago.
        var notExpiredUserAuthRequest = await authRequestRepository.CreateAsync(
            CreateAuthRequest(user.Id, AuthRequestType.Unlock, DateTime.UtcNow.AddMinutes(-1)));

        // An AdminApproval AuthRequest that was create 6 days 23 hours 59 minutes 59 seconds ago which is right on the edge of still being valid
        var notExpiredAdminApprovalRequest = await authRequestRepository.CreateAsync(
            CreateAuthRequest(user.Id, AuthRequestType.AdminApproval, DateTime.UtcNow.Add(new TimeSpan(days: 6, hours: 23, minutes: 59, seconds: 59))));

        // An AdminApproval AuthRequest that was created a week ago but just approved 11 hours ago.
        var notExpiredApprovedAdminApprovalRequest = await authRequestRepository.CreateAsync(
            CreateAuthRequest(user.Id, AuthRequestType.AdminApproval, DateTime.UtcNow.AddDays(7), true, DateTime.UtcNow.AddHours(11)));

        var numberOfDeleted = await authRequestRepository.DeleteExpiredAsync(_userRequestExpiration, _adminRequestExpiration, _afterAdminApprovalExpiration);

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

    [DatabaseTheory, DatabaseData]
    public async Task UpdateManyAsync_Works(
        IAuthRequestRepository authRequestRepository,
        IUserRepository userRepository)
    {
        // Create two distinct real users for foreign key requirements
        var user1 = await userRepository.CreateAsync(new User
        {
            Name = "First Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var user2 = await userRepository.CreateAsync(new User
        {
            Name = "Second Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var user3 = await userRepository.CreateAsync(new User
        {
            Name = "Third Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        // Create two different and still valid (not expired or responded to) auth requests
        var authRequests = new List<AuthRequest>
        {
            await authRequestRepository.CreateAsync(CreateAuthRequest(user1.Id, AuthRequestType.AdminApproval, DateTime.UtcNow.AddMinutes(-5))),
            await authRequestRepository.CreateAsync(CreateAuthRequest(user3.Id, AuthRequestType.AdminApproval, DateTime.UtcNow.AddMinutes(-7))),
            await authRequestRepository.CreateAsync(CreateAuthRequest(user2.Id, AuthRequestType.AdminApproval, DateTime.UtcNow.AddMinutes(-10))),
            // This last auth request is not created manually, and will be
            // used to make sure entity framework's `UpdateRange` method
            // doesn't create requests too.
            CreateAuthRequest(user2.Id, AuthRequestType.AdminApproval, DateTime.UtcNow.AddMinutes(-11))
        };

        // Update some properties on two auth request, but leave the other one
        // alone to be a control value
        var authRequestToBeUpdated1 = authRequests[0];
        var authRequestToBeUpdated2 = authRequests[1];
        var authRequestNotToBeUpdated = authRequests[2];
        authRequests[0].Approved = true;
        authRequests[0].ResponseDate = DateTime.UtcNow.AddMinutes(-1);
        authRequests[0].Key = "UPDATED_KEY_1";
        authRequests[0].MasterPasswordHash = "UPDATED_MASTERPASSWORDHASH_1";

        authRequests[1].Approved = false;
        authRequests[1].ResponseDate = DateTime.UtcNow.AddMinutes(-2);

        // Run the method being tested
        await authRequestRepository.UpdateManyAsync(authRequests);

        // Define what "Equality" really means in this context
        // This includes stripping milliseconds off of dates, because we can't
        // reliably compare that deep
        static DateTime? TrimMilliseconds(DateTime? dt)
        {
            if (!dt.HasValue)
            {
                return null;
            }
            return new DateTime(dt.Value.Year, dt.Value.Month, dt.Value.Day, dt.Value.Hour, dt.Value.Minute, dt.Value.Second, 0, dt.Value.Kind);
        }

        bool AuthRequestEquals(AuthRequest x, AuthRequest y)
        {
            return
            x.Id == y.Id &&
            x.UserId == y.UserId &&
            x.Type == y.Type &&
            x.RequestDeviceIdentifier == y.RequestDeviceIdentifier &&
            x.RequestDeviceType == y.RequestDeviceType &&
            x.RequestIpAddress == y.RequestIpAddress &&
            x.ResponseDeviceId == y.ResponseDeviceId &&
            x.AccessCode == y.AccessCode &&
            x.PublicKey == y.PublicKey &&
            x.Key == y.Key &&
            x.MasterPasswordHash == y.MasterPasswordHash &&
            x.Approved == y.Approved &&
            TrimMilliseconds(x.CreationDate) == TrimMilliseconds(y.CreationDate) &&
            TrimMilliseconds(x.ResponseDate) == TrimMilliseconds(y.ResponseDate) &&
            TrimMilliseconds(x.AuthenticationDate) == TrimMilliseconds(y.AuthenticationDate) &&
            x.OrganizationId == y.OrganizationId;
        }

        // Assert that the unchanged auth request is still unchanged
        var skippedAuthRequest = await authRequestRepository.GetByIdAsync(authRequestNotToBeUpdated.Id);
        Assert.NotNull(skippedAuthRequest);
        Assert.True(AuthRequestEquals(skippedAuthRequest, authRequestNotToBeUpdated));

        // Assert that the values updated on the changed auth requests were updated, and no others
        var updatedAuthRequest1 = await authRequestRepository.GetByIdAsync(authRequestToBeUpdated1.Id);
        Assert.NotNull(updatedAuthRequest1);
        Assert.True(AuthRequestEquals(authRequestToBeUpdated1, updatedAuthRequest1));
        var updatedAuthRequest2 = await authRequestRepository.GetByIdAsync(authRequestToBeUpdated2.Id);
        Assert.NotNull(updatedAuthRequest2);
        Assert.True(AuthRequestEquals(authRequestToBeUpdated2, updatedAuthRequest2));

        // Assert that the auth request we never created is not created by
        // the update method.
        var uncreatedAuthRequest = await authRequestRepository.GetByIdAsync(authRequests[3].Id);
        Assert.Null(uncreatedAuthRequest);
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
