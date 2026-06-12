using System.ComponentModel.DataAnnotations;
using Bit.Admin.Billing.Models.OrganizationPlanMigrationCohorts;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;

namespace Admin.Test.Billing.Models;

public class CohortFormModelTests
{
    [Fact]
    public void GetMigrationPathId_RegisteredByte_ReturnsCastedEnum()
    {
        var model = new CohortFormModel { Name = "C", MigrationPathSelection = "1" };

        Assert.Equal(MigrationPathId.Enterprise2020AnnualToCurrent, model.GetMigrationPathId());
    }

    private static ValidationContext Ctx(object o) => new(o);

    [Fact]
    public void Validate_PlaceholderSelection_RejectsOnMigrationPathSelection()
    {
        var model = new CohortFormModel { Name = "A", MigrationPathSelection = "" };

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, Ctx(model), results, validateAllProperties: true);

        Assert.Contains(results, r =>
            r.MemberNames.Contains(nameof(CohortFormModel.MigrationPathSelection))
            && r.ErrorMessage!.Contains("Please select a migration path or None."));
    }

    [Fact]
    public void Validate_ChurnOnlyWithProactiveCoupon_RejectsOnProactiveField()
    {
        var model = new CohortFormModel
        {
            Name = "A",
            MigrationPathSelection = "none",
            ProactiveDiscountCouponCode = "PROACTIVE10",
            ChurnDiscountCouponCode = "CHURN15",
        };

        var results = model.Validate(Ctx(model)).ToList();

        Assert.Contains(results, r =>
            r.MemberNames.Contains(nameof(CohortFormModel.ProactiveDiscountCouponCode))
            && r.ErrorMessage!.Contains("Churn-only cohorts cannot have a proactive discount coupon."));
    }

    [Fact]
    public void Validate_ChurnOnlyWithoutChurnCoupon_RejectsOnChurnField()
    {
        var model = new CohortFormModel
        {
            Name = "A",
            MigrationPathSelection = "none",
            ProactiveDiscountCouponCode = null,
            ChurnDiscountCouponCode = null,
        };

        var results = model.Validate(Ctx(model)).ToList();

        Assert.Contains(results, r =>
            r.MemberNames.Contains(nameof(CohortFormModel.ChurnDiscountCouponCode))
            && r.ErrorMessage!.Contains("Churn discount coupon is required for Churn-only cohorts."));
    }

    [Theory]
    [InlineData("1", null, null)]
    [InlineData("1", "PROACTIVE", "CHURN15")]
    [InlineData("none", null, "SAVE15")]
    public void Validate_ValidShape_HasNoErrors(string selection, string? proactive, string? churn)
    {
        var model = new CohortFormModel
        {
            Name = "A",
            MigrationPathSelection = selection,
            ProactiveDiscountCouponCode = proactive,
            ChurnDiscountCouponCode = churn,
        };

        Assert.Empty(model.Validate(Ctx(model)));
    }
}
