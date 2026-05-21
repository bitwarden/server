using System.ComponentModel.DataAnnotations;
using Bit.Admin.Billing.Models.Cohorts;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Xunit;

namespace Admin.Test.Billing.Models;

public class CohortFormModelTests
{
    [Fact]
    public void GetMigrationPathId_RegisteredByte_ReturnsCastedEnum()
    {
        var model = new CohortFormModel { Name = "C", MigrationPathSelection = "1" };

        Assert.Equal(MigrationPathId.Enterprise2020AnnualToCurrent, model.GetMigrationPathId());
    }
}
