using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.PlanMigration.Queries;

[SutProviderCustomize]
public class GetCohortAssignmentStateQueryTests
{
    [Theory]
    [BitAutoData(0, false)]
    [BitAutoData(1, true)]
    [BitAutoData(99, true)]
    public async Task Run_ReturnsRecordCarryingCountAndDerivedPredicate(
        int repoCount,
        bool expectedHasNonPending,
        OrganizationPlanMigrationCohort cohort,
        SutProvider<GetCohortAssignmentStateQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetCohortNonPendingAssignmentsCountAsync(cohort.Id)
            .Returns(repoCount);

        var result = await sutProvider.Sut.Run(cohort);

        Assert.Equal(repoCount, result.NonPendingAssignmentCount);
        Assert.Equal(expectedHasNonPending, result.HasNonPendingAssignments);
    }
}
