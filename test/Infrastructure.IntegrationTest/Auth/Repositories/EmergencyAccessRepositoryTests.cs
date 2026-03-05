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
    public async Task DeleteManyAsync_DeletesMultipleGranteeRecordsAsync(
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

        var invitedGranteeUser2 = await userRepository.CreateAsync(new User
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
            GranteeId = invitedGranteeUser2.Id,
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
        foreach (var grantee in (List<User>)[confirmedGranteeUser1, invitedGranteeUser2, granteeUser3])
        {
            var granteeEmergencyAccess = await emergencyAccessRepository.GetManyDetailsByGranteeIdAsync(grantee.Id);
            Assert.Empty(granteeEmergencyAccess);
        }
    }

    /// <summary>
    /// Verifies GetManyDetailsByUserIdsAsync returns all emergency access records
    /// where the user IDs are either grantors or grantees.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetManyDetailsByUserIdsAsync_ReturnsAllMatchingRecords(
        IUserRepository userRepository,
        IEmergencyAccessRepository emergencyAccessRepository)
    {
        // Arrange - Create users
        var grantorUser1 = await userRepository.CreateAsync(new User
        {
            Name = "Grantor 1",
            Email = $"test+grantor1{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var grantorUser2 = await userRepository.CreateAsync(new User
        {
            Name = "Grantor 2",
            Email = $"test+grantor2{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var granteeUser1 = await userRepository.CreateAsync(new User
        {
            Name = "Grantee 1",
            Email = $"test+grantee1{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var granteeUser2 = await userRepository.CreateAsync(new User
        {
            Name = "Grantee 2",
            Email = $"test+grantee2{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        // Create emergency access records
        // Grantor1 -> Grantee1 (matches both queried IDs)
        var ea1 = await emergencyAccessRepository.CreateAsync(new EmergencyAccess
        {
            GrantorId = grantorUser1.Id,
            GranteeId = granteeUser1.Id,
            Status = EmergencyAccessStatusType.Confirmed,
        });

        // Grantor2 -> Grantee1 (matches via grantee)
        var ea2 = await emergencyAccessRepository.CreateAsync(new EmergencyAccess
        {
            GrantorId = grantorUser2.Id,
            GranteeId = granteeUser1.Id,
            Status = EmergencyAccessStatusType.Confirmed,
        });

        // Grantor1 -> Grantee2 (matches via grantor)
        var ea3 = await emergencyAccessRepository.CreateAsync(new EmergencyAccess
        {
            GrantorId = grantorUser1.Id,
            GranteeId = granteeUser2.Id,
            Status = EmergencyAccessStatusType.Accepted,
        });

        // Grantor2 -> Grantee2 (should NOT be returned - neither user is in the query)
        await emergencyAccessRepository.CreateAsync(new EmergencyAccess
        {
            GrantorId = grantorUser2.Id,
            GranteeId = granteeUser2.Id,
            Status = EmergencyAccessStatusType.Confirmed,
        });

        // Act - Query with Grantor1 and Grantee1 user IDs
        var userIds = new List<Guid> { grantorUser1.Id, granteeUser1.Id };
        var results = await emergencyAccessRepository.GetManyDetailsByUserIdsAsync(userIds);

        // Assert - Should return exactly the 3 records involving Grantor1 or Grantee1:
        // - Grantor1 -> Grantee1 (matches both, returned once)
        // - Grantor2 -> Grantee1 (matches via grantee)
        // - Grantor1 -> Grantee2 (matches via grantor)
        Assert.NotNull(results);
        Assert.Equal(3, results.Count);

        var resultIds = results.Select(r => r.Id).ToHashSet();
        Assert.Contains(ea1.Id, resultIds);
        Assert.Contains(ea2.Id, resultIds);
        Assert.Contains(ea3.Id, resultIds);
    }

    /// <summary>
    /// Verifies GetManyDetailsByUserIdsAsync handles an empty list gracefully
    /// and returns an empty collection.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetManyDetailsByUserIdsAsync_HandlesEmptyList(
        IEmergencyAccessRepository emergencyAccessRepository)
    {
        // Act
        var results = await emergencyAccessRepository.GetManyDetailsByUserIdsAsync([]);

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    /// <summary>
    /// Verifies GetManyDetailsByUserIdsAsync includes full details from both
    /// grantor and grantee users (emails, names populated via JOIN).
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetManyDetailsByUserIdsAsync_IncludesDetailsFromBothUsers(
        IUserRepository userRepository,
        IEmergencyAccessRepository emergencyAccessRepository)
    {
        // Arrange
        var grantorEmail = $"test+grantor{Guid.NewGuid()}@email.com";
        var granteeEmail = $"test+grantee{Guid.NewGuid()}@email.com";

        var grantorUser = await userRepository.CreateAsync(new User
        {
            Name = "Grantor Name",
            Email = grantorEmail,
            AvatarColor = "#ff0000",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var granteeUser = await userRepository.CreateAsync(new User
        {
            Name = "Grantee Name",
            Email = granteeEmail,
            AvatarColor = "#0000ff",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        await emergencyAccessRepository.CreateAsync(new EmergencyAccess
        {
            GrantorId = grantorUser.Id,
            GranteeId = granteeUser.Id,
            Status = EmergencyAccessStatusType.Confirmed,
        });

        // Act
        var results = await emergencyAccessRepository.GetManyDetailsByUserIdsAsync([grantorUser.Id]);

        // Assert
        Assert.NotNull(results);
        Assert.Single(results);

        var record = results.First();
        Assert.Equal(grantorEmail, record.GrantorEmail);
        Assert.Equal(granteeEmail, record.GranteeEmail);
        Assert.Equal("Grantor Name", record.GrantorName);
        Assert.Equal("Grantee Name", record.GranteeName);
        Assert.Equal("#ff0000", record.GrantorAvatarColor);
        Assert.Equal("#0000ff", record.GranteeAvatarColor);
    }

    /// <summary>
    /// Verifies GetManyDetailsByUserIdsAsync returns records when the queried user ID
    /// appears only as a grantee (not as a grantor in any record).
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetManyDetailsByUserIdsAsync_GranteeOnlyQuery_ReturnsMatchingRecordsAsync(
        IUserRepository userRepository,
        IEmergencyAccessRepository emergencyAccessRepository)
    {
        // Arrange
        var grantorUser = await userRepository.CreateAsync(new User
        {
            Name = "Grantor",
            Email = $"test+grantor{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var granteeUser = await userRepository.CreateAsync(new User
        {
            Name = "Grantee",
            Email = $"test+grantee{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var unrelatedUser = await userRepository.CreateAsync(new User
        {
            Name = "Unrelated",
            Email = $"test+unrelated{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var expectedRecord = await emergencyAccessRepository.CreateAsync(new EmergencyAccess
        {
            GrantorId = grantorUser.Id,
            GranteeId = granteeUser.Id,
            Status = EmergencyAccessStatusType.Confirmed,
        });

        // Record that should NOT be returned - granteeUser is not involved
        await emergencyAccessRepository.CreateAsync(new EmergencyAccess
        {
            GrantorId = grantorUser.Id,
            GranteeId = unrelatedUser.Id,
            Status = EmergencyAccessStatusType.Confirmed,
        });

        // Act - query using only the grantee's ID; granteeUser has no grantor records
        var results = await emergencyAccessRepository.GetManyDetailsByUserIdsAsync([granteeUser.Id]);

        // Assert
        Assert.NotNull(results);
        Assert.Single(results);
        Assert.Equal(expectedRecord.Id, results.First().Id);
    }

    /// <summary>
    /// Verifies GetDetailsByIdAsync returns the correct EmergencyAccessDetails record,
    /// including email and name fields populated via the view JOIN.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetDetailsByIdAsync_ReturnsDetails_WhenRecordExistsAsync(
        IUserRepository userRepository,
        IEmergencyAccessRepository emergencyAccessRepository)
    {
        // Arrange
        var grantorEmail = $"test+grantor{Guid.NewGuid()}@email.com";
        var granteeEmail = $"test+grantee{Guid.NewGuid()}@email.com";

        var grantorUser = await userRepository.CreateAsync(new User
        {
            Name = "Grantor Name",
            Email = grantorEmail,
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var granteeUser = await userRepository.CreateAsync(new User
        {
            Name = "Grantee Name",
            Email = granteeEmail,
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var ea = await emergencyAccessRepository.CreateAsync(new EmergencyAccess
        {
            GrantorId = grantorUser.Id,
            GranteeId = granteeUser.Id,
            Status = EmergencyAccessStatusType.Confirmed,
        });

        // Act
        var result = await emergencyAccessRepository.GetDetailsByIdAsync(ea.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ea.Id, result.Id);
        Assert.Equal(grantorEmail, result.GrantorEmail);
        Assert.Equal(granteeEmail, result.GranteeEmail);
    }

    /// <summary>
    /// Verifies GetDetailsByIdAsync returns null when no record matches the given ID.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetDetailsByIdAsync_ReturnsNull_WhenRecordDoesNotExistAsync(
        IEmergencyAccessRepository emergencyAccessRepository)
    {
        // Act
        var result = await emergencyAccessRepository.GetDetailsByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Verifies GetManyDetailsByUserIdsAsync returns invited emergency access records
    /// (where GranteeId is null and only Email is set) when querying by grantor ID,
    /// and that GranteeEmail falls back to the invite email address.
    /// </summary>
    [DatabaseTheory, DatabaseData]
    public async Task GetManyDetailsByUserIdsAsync_InvitedRecord_ReturnedByGrantorIdAsync(
        IUserRepository userRepository,
        IEmergencyAccessRepository emergencyAccessRepository)
    {
        // Arrange
        var grantorUser = await userRepository.CreateAsync(new User
        {
            Name = "Grantor",
            Email = $"test+grantor{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var inviteEmail = $"test+invited{Guid.NewGuid()}@email.com";

        var invitedRecord = await emergencyAccessRepository.CreateAsync(new EmergencyAccess
        {
            GrantorId = grantorUser.Id,
            GranteeId = null,
            Email = inviteEmail,
            Status = EmergencyAccessStatusType.Invited,
        });

        // Act
        var results = await emergencyAccessRepository.GetManyDetailsByUserIdsAsync([grantorUser.Id]);

        // Assert
        Assert.NotNull(results);
        Assert.Single(results);

        var record = results.First();
        Assert.Equal(invitedRecord.Id, record.Id);
        Assert.Null(record.GranteeId);
        Assert.Equal(inviteEmail, record.GranteeEmail); // falls back to EA.Email when no registered grantee
        Assert.Null(record.GranteeName);
        Assert.Null(record.GranteeAvatarColor);
    }
}
