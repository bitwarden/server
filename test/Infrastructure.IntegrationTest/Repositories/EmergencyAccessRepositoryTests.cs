using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Repositories;

public class EmergencyAccessRepositoriesTests
{
    [DatabaseTheory, DatabaseData]
    public async Task DeleteAsync_UpdatesRevisionDate(IUserRepository userRepository,
      IEmergencyAccessRepository emergencyAccessRepository,
      ITestDatabaseHelper helper)
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

        helper.ClearTracker();

        await emergencyAccessRepository.DeleteAsync(emergencyAccess);

        var updatedGrantee = await userRepository.GetByIdAsync(granteeUser.Id);

        Assert.NotEqual(updatedGrantee.AccountRevisionDate, granteeUser.AccountRevisionDate);
    }
}
