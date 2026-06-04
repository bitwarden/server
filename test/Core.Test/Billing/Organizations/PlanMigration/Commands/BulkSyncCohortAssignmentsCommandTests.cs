using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Commands;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.Utilities;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.PlanMigration.Commands;

[SutProviderCustomize]
public class BulkSyncCohortAssignmentsCommandTests
{
    private static readonly Guid _org = Guid.NewGuid();
    private static readonly Guid _cohortId = Guid.NewGuid();
    private static readonly IFormFile _file = Substitute.For<IFormFile>();

    private static RawCohortBulkAssignmentRow Row(int line, Guid org, string cohort) =>
        new(line, org.ToString(), cohort);

    private static void ArrangeParse(
        SutProvider<BulkSyncCohortAssignmentsCommand> sut,
        IReadOnlyList<RawCohortBulkAssignmentRow> rows,
        params CohortBulkAssignmentError[] parseErrors) =>
        sut.GetDependency<ICohortBulkAssignmentCsvParser>()
            .Parse(Arg.Any<IFormFile>())
            .Returns(new CohortBulkAssignmentParseResult(rows, parseErrors));

    private static void ArrangeOrg(SutProvider<BulkSyncCohortAssignmentsCommand> sut, Guid id, PlanType plan) =>
        sut.GetDependency<IOrganizationRepository>()
            .GetPlanTypesByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([new OrganizationPlanType { OrganizationId = id, PlanType = plan }]);

    private static void ArrangeCohort(SutProvider<BulkSyncCohortAssignmentsCommand> sut, OrganizationPlanMigrationCohort cohort) =>
        sut.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetManyByNamesAsync(Arg.Any<IEnumerable<string>>())
            .Returns([cohort]);

    [Theory, BitAutoData]
    public async Task Run_UnknownOrg_ReturnsExistenceError(
        SutProvider<BulkSyncCohortAssignmentsCommand> sutProvider)
    {
        ArrangeParse(sutProvider, [Row(2, _org, "A1")]);
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetPlanTypesByOrganizationIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([]);
        ArrangeCohort(sutProvider, new OrganizationPlanMigrationCohort { Id = _cohortId, Name = "A1" });

        var result = await sutProvider.Sut.Run(_file);

        Assert.True(result.Success);
        var value = result.AsT0;
        Assert.False(value.Succeeded);
        Assert.Contains(value.Errors, e => e.LineNumber == 2 && e.Message.Contains("does not exist"));
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs().SyncManyAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task Run_UnknownCohort_ReturnsResolutionError(
        SutProvider<BulkSyncCohortAssignmentsCommand> sutProvider)
    {
        ArrangeParse(sutProvider, [Row(2, _org, "Nope")]);
        ArrangeOrg(sutProvider, _org, PlanType.EnterpriseAnnually2020);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetManyByNamesAsync(Arg.Any<IEnumerable<string>>())
            .Returns([]);

        var result = await sutProvider.Sut.Run(_file);

        var value = result.AsT0;
        Assert.False(value.Succeeded);
        Assert.Contains(value.Errors, e => e.LineNumber == 2 && e.Message.Contains("does not match any cohort"));
    }

    [Theory, BitAutoData]
    public async Task Run_DuplicateOrg_ReturnsError(
        SutProvider<BulkSyncCohortAssignmentsCommand> sutProvider)
    {
        ArrangeParse(sutProvider, [Row(2, _org, "A1"), Row(3, _org, "A1")]);
        ArrangeOrg(sutProvider, _org, PlanType.EnterpriseAnnually2020);
        ArrangeCohort(sutProvider, new OrganizationPlanMigrationCohort { Id = _cohortId, Name = "A1" });

        var result = await sutProvider.Sut.Run(_file);

        var value = result.AsT0;
        Assert.False(value.Succeeded);
        Assert.Contains(value.Errors, e => e.LineNumber == 3 && e.Message.Contains("appears more than once"));
    }

    [Theory, BitAutoData]
    public async Task Run_Clean_CommitsAndCountsPlanMismatch(
        SutProvider<BulkSyncCohortAssignmentsCommand> sutProvider)
    {
        ArrangeParse(sutProvider, [Row(2, _org, "A1")]);
        ArrangeOrg(sutProvider, _org, PlanType.EnterpriseMonthly2020);
        ArrangeCohort(sutProvider, new OrganizationPlanMigrationCohort
        {
            Id = _cohortId,
            Name = "A1",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
        });
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .SyncManyAsync(Arg.Any<IEnumerable<ResolvedCohortBulkAssignmentRow>>())
            .Returns(new CohortBulkAssignmentSummary { Inserted = 1 });

        var result = await sutProvider.Sut.Run(_file);

        Assert.True(result.Success);
        var value = result.AsT0;
        Assert.True(value.Succeeded);
        Assert.Equal(1, value.Summary!.Inserted);
        Assert.Equal(1, value.Summary!.PlanMismatch);
    }

    [Theory, BitAutoData]
    public async Task Run_RepositoryThrows_ReturnsUnhandled(
        SutProvider<BulkSyncCohortAssignmentsCommand> sutProvider)
    {
        ArrangeParse(sutProvider, [Row(2, _org, "A1")]);
        ArrangeOrg(sutProvider, _org, PlanType.EnterpriseAnnually2020);
        ArrangeCohort(sutProvider, new OrganizationPlanMigrationCohort { Id = _cohortId, Name = "A1" });
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .SyncManyAsync(Arg.Any<IEnumerable<ResolvedCohortBulkAssignmentRow>>())
            .Returns<CohortBulkAssignmentSummary>(_ => throw new Exception("DB unavailable"));

        var result = await sutProvider.Sut.Run(_file);

        Assert.False(result.Success);
        Assert.True(result.IsT3);
    }
}
