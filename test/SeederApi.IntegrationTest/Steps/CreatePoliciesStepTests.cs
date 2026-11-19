using Bit.Core.AdminConsole.Enums;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Steps;
using Xunit;
using static Bit.SeederApi.IntegrationTest.Steps.SeederStepTestHelpers;

namespace Bit.SeederApi.IntegrationTest.Steps;

public class CreatePoliciesStepTests
{
    private const string _transferEnabledData = """{"enableIndividualItemsTransfer":true}""";

    [Fact]
    public void Execute_PolicyEnterpriseList_AddsOneEnabledRowPerTypeForTheOrganization()
    {
        PolicyType[] types =
        [
            PolicyType.MasterPassword,
            PolicyType.PasswordGenerator,
            PolicyType.SingleOrg,
            PolicyType.SendOptions,
            PolicyType.ResetPassword,
            PolicyType.MaximumVaultTimeout,
            PolicyType.DisablePersonalVaultExport,
        ];
        var context = NewContext(new SeederSettings());
        PreloadOrganization(context);

        new CreatePoliciesStep(types.Select(t => (t, true, (string?)null)).ToList()).Execute(context);

        Assert.Equal(types, context.Policies.Select(p => p.Type));
        Assert.All(context.Policies, p =>
        {
            Assert.True(p.Enabled);
            Assert.Equal(context.Organization!.Id, p.OrganizationId);
        });
    }

    [Fact]
    public void Execute_OrganizationDataOwnershipWithoutData_WritesTransferEnabledData()
    {
        var context = NewContext(new SeederSettings());
        PreloadOrganization(context);

        new CreatePoliciesStep([(PolicyType.OrganizationDataOwnership, true, null)]).Execute(context);

        Assert.Equal(_transferEnabledData, Assert.Single(context.Policies).Data);
    }

    [Fact]
    public void Execute_ExplicitData_OverridesDefault()
    {
        const string data = """{"enableIndividualItemsTransfer":false}""";
        var context = NewContext(new SeederSettings());
        PreloadOrganization(context);

        new CreatePoliciesStep([(PolicyType.OrganizationDataOwnership, true, data)]).Execute(context);

        Assert.Equal(data, Assert.Single(context.Policies).Data);
    }
}
