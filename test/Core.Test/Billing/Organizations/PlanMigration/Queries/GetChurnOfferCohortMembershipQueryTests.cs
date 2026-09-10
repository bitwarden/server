using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.PlanMigration.Queries;

public class GetChurnOfferCohortMembershipQueryTests
{
    private const string ChurnCouponCode = "churn-15-percent-once";

    private readonly IOrganizationPlanMigrationCohortAssignmentRepository _assignmentRepository =
        Substitute.For<IOrganizationPlanMigrationCohortAssignmentRepository>();
    private readonly IOrganizationPlanMigrationCohortRepository _cohortRepository =
        Substitute.For<IOrganizationPlanMigrationCohortRepository>();
    private readonly GetChurnOfferCohortMembershipQuery _query;

    public GetChurnOfferCohortMembershipQueryTests()
    {
        _query = new GetChurnOfferCohortMembershipQuery(_assignmentRepository, _cohortRepository);
    }

    [Fact]
    public async Task Run_NoAssignment_ReturnsNull()
    {
        var organization = new Organization { Id = Guid.NewGuid() };
        _assignmentRepository.GetByOrganizationIdAsync(organization.Id)
            .Returns((OrganizationPlanMigrationCohortAssignment?)null);

        var result = await _query.Run(organization);

        Assert.Null(result);
        await _cohortRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Run_CohortInactive_ReturnsNull()
    {
        var organization = new Organization { Id = Guid.NewGuid() };
        var cohortId = Guid.NewGuid();
        _assignmentRepository.GetByOrganizationIdAsync(organization.Id).Returns(new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = cohortId
        });
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "inactive-cohort",
            ChurnDiscountCouponCode = ChurnCouponCode,
            IsActive = false
        });

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_NullChurnCouponCode_ReturnsNull()
    {
        var organization = new Organization { Id = Guid.NewGuid() };
        var cohortId = Guid.NewGuid();
        _assignmentRepository.GetByOrganizationIdAsync(organization.Id).Returns(new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = cohortId
        });
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "no-coupon-cohort",
            ChurnDiscountCouponCode = null,
            IsActive = true
        });

        var result = await _query.Run(organization);

        Assert.Null(result);
    }

    [Fact]
    public async Task Run_ActiveCohortWithCoupon_ReturnsMembership()
    {
        var organization = new Organization { Id = Guid.NewGuid() };
        var cohortId = Guid.NewGuid();
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = cohortId
        };
        var cohort = new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = "active-cohort",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
            ChurnDiscountCouponCode = ChurnCouponCode,
            IsActive = true
        };
        _assignmentRepository.GetByOrganizationIdAsync(organization.Id).Returns(assignment);
        _cohortRepository.GetByIdAsync(cohortId).Returns(cohort);

        var result = await _query.Run(organization);

        Assert.NotNull(result);
        Assert.Same(assignment, result.Assignment);
        Assert.Same(cohort, result.Cohort);
    }
}
