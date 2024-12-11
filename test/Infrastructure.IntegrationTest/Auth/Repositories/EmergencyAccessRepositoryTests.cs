using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Auth.Repositories;

public class EmergencyAccessRepositoriesTests
{
    [DatabaseTheory, DatabaseData]
    public async Task DeleteAsync_UpdatesRevisionDate(
        IUserRepository userRepository,
        IEmergencyAccessRepository emergencyAccessRepository
    )
    {
        var grantorUser = await userRepository.CreateAsync(
            new User
            {
                Name = "Test Grantor User",
                Email = $"test+grantor{Guid.NewGuid()}@email.com",
                ApiKey = "TEST",
                SecurityStamp = "stamp",
            }
        );

        var granteeUser = await userRepository.CreateAsync(
            new User
            {
                Name = "Test Grantee User",
                Email = $"test+grantee{Guid.NewGuid()}@email.com",
                ApiKey = "TEST",
                SecurityStamp = "stamp",
            }
        );

        var emergencyAccess = await emergencyAccessRepository.CreateAsync(
            new EmergencyAccess
            {
                GrantorId = grantorUser.Id,
                GranteeId = granteeUser.Id,
                Status = EmergencyAccessStatusType.Confirmed,
            }
        );

        await emergencyAccessRepository.DeleteAsync(emergencyAccess);

        var updatedGrantee = await userRepository.GetByIdAsync(granteeUser.Id);

        Assert.NotNull(updatedGrantee);
        Assert.NotEqual(updatedGrantee.AccountRevisionDate, granteeUser.AccountRevisionDate);
    }
}
