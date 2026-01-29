using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Auth.Repositories;

public class EmergencyAccessRepositoriesTests
{
    [DatabaseTheory, DatabaseData]
    public async Task DeleteAsync_UpdatesRevisionDate(IUserRepository userRepository,
      IEmergencyAccessRepository emergencyAccessRepository)
    {
        var grantorUser = await userRepository.CreateAsync(new User
        {
            Name = "Test Grantor User",
            Email = $"test+grantor{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var granteeUser = await userRepository.CreateAsync(new User
        {
            Name = "Test Grantee User",
            Email = $"test+grantee{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var emergencyAccess = await emergencyAccessRepository.CreateAsync(new EmergencyAccess
        {
            GrantorId = grantorUser.Id,
            GranteeId = granteeUser.Id,
            Status = EmergencyAccessStatusType.Confirmed,
        });

        await emergencyAccessRepository.DeleteAsync(emergencyAccess);

        var updatedGrantee = await userRepository.GetByIdAsync(granteeUser.Id);

        Assert.NotNull(updatedGrantee);
        Assert.NotEqual(updatedGrantee.AccountRevisionDate, granteeUser.AccountRevisionDate);
    }

    /// <summary>
    /// Creates 3 Emergency Access records all connected to a single grantor, but separate grantees.
    /// All 3 records are then deleted in a single call to DeleteManyAsync.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task DeleteManyAsync_DeletesMultipleGranteeRecords_UpdatesUserRevisionDates(
        IUserRepository userRepository,
        IEmergencyAccessRepository emergencyAccessRepository)
    {
        // Arrange
        var grantorUser = await userRepository.CreateAsync(new User
        {
            Name = "Test Grantor User",
            Email = $"test+grantor{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var confirmedGranteeUser1 = await userRepository.CreateAsync(new User
        {
            Name = "Test Grantee User 1",
            Email = $"test+grantee{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var granteeUser2 = await userRepository.CreateAsync(new User
        {
            Name = "Test Grantee User 2",
            Email = $"test+grantee{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var granteeUser3 = await userRepository.CreateAsync(new User
        {
            Name = "Test Grantee User 3",
            Email = $"test+grantee{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var confirmedEmergencyAccess = await emergencyAccessRepository.CreateAsync(new EmergencyAccess
        {
            GrantorId = grantorUser.Id,
            GranteeId = confirmedGranteeUser1.Id,
            Status = EmergencyAccessStatusType.Confirmed,
        });

        var invitedEmergencyAccess = await emergencyAccessRepository.CreateAsync(new EmergencyAccess
        {
            GrantorId = grantorUser.Id,
            GranteeId = granteeUser2.Id,
            Status = EmergencyAccessStatusType.Invited,
        });

        var acceptedEmergencyAccess = await emergencyAccessRepository.CreateAsync(new EmergencyAccess
        {
            GrantorId = grantorUser.Id,
            GranteeId = granteeUser3.Id,
            Status = EmergencyAccessStatusType.Accepted,
        });

        // Act
        await emergencyAccessRepository.DeleteManyAsync([confirmedEmergencyAccess.Id, invitedEmergencyAccess.Id, acceptedEmergencyAccess.Id]);

        // Assert
        // ensure Grantor records deleted
        var grantorEmergencyAccess = await emergencyAccessRepository.GetManyDetailsByGrantorIdAsync(grantorUser.Id);
        Assert.Empty(grantorEmergencyAccess);

        // ensure Grantee records deleted
        foreach (User grantee in (List<User>)[confirmedGranteeUser1, granteeUser2, granteeUser3])
        {
            var granteeEmergencyAccess = await emergencyAccessRepository.GetManyDetailsByGranteeIdAsync(grantee.Id);
            Assert.Empty(granteeEmergencyAccess);
        }

        // Only the Status.Confirmed grantee's AccountRevisionDate should be updated
        var updatedGrantee = await userRepository.GetByIdAsync(confirmedGranteeUser1.Id);
        Assert.NotNull(updatedGrantee);
        Assert.NotEqual(updatedGrantee.AccountRevisionDate, confirmedGranteeUser1.AccountRevisionDate);
    }
}
