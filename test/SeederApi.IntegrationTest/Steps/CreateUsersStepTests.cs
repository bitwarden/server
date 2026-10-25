using Bit.Core.Enums;
using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Steps;
using Xunit;
using static Bit.SeederApi.IntegrationTest.Steps.SeederStepTestHelpers;

namespace Bit.SeederApi.IntegrationTest.Steps;

public class CreateUsersStepTests
{
    // Invited comes first so the leading members are not Confirmed and must not take the Admin slots.
    private static readonly Distribution<OrganizationUserStatusType> _statusMix = new(
        (OrganizationUserStatusType.Invited, 0.25),
        (OrganizationUserStatusType.Confirmed, 0.5),
        (OrganizationUserStatusType.Accepted, 0),
        (OrganizationUserStatusType.Revoked, 0.25));

    [Fact]
    public void Execute_StatusMix_AssignsExactStatusCounts()
    {
        var context = ExecuteWithStatusMixAndTwoAdmins();

        var byStatus = context.OrganizationUsers.GroupBy(ou => ou.Status).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(10, byStatus[OrganizationUserStatusType.Confirmed]);
        Assert.Equal(5, byStatus[OrganizationUserStatusType.Invited]);
        Assert.False(byStatus.ContainsKey(OrganizationUserStatusType.Accepted));
        Assert.Equal(5, byStatus[OrganizationUserStatusType.Revoked]);
    }

    [Fact]
    public void Execute_AdminCount_MakesThatManyConfirmedMembersAdmins()
    {
        var context = ExecuteWithStatusMixAndTwoAdmins();

        var admins = context.OrganizationUsers.Where(ou => ou.Type == OrganizationUserType.Admin).ToList();
        Assert.Equal(2, admins.Count);
        Assert.All(admins, a => Assert.Equal(OrganizationUserStatusType.Confirmed, a.Status));
        Assert.All(
            context.OrganizationUsers.Except(admins),
            ou => Assert.Equal(OrganizationUserType.User, ou.Type));
    }

    [Fact]
    public void Execute_StatusMix_RegistersOnlyConfirmedMembersAsDigests()
    {
        var context = ExecuteWithStatusMixAndTwoAdmins();

        var confirmedIds = context.OrganizationUsers
            .Where(ou => ou.Status == OrganizationUserStatusType.Confirmed)
            .Select(ou => ou.Id);
        Assert.Equal(confirmedIds, context.Registry.UserDigests.Select(d => d.OrgUserId));
    }

    private static SeederContext ExecuteWithStatusMixAndTwoAdmins()
    {
        var context = NewContext(new SeederSettings());
        PreloadOrganization(context);

        new CreateUsersStep(20, statusMix: _statusMix, adminCount: 2).Execute(context);

        return context;
    }
}
