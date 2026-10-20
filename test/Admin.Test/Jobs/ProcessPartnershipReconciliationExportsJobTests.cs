using Bit.Admin.Jobs;
using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Test.Common.AutoFixture;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using Quartz;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Admin.Test.Jobs;

public class ProcessPartnershipReconciliationExportsJobTests
{
    [Fact]
    public async Task Execute_FeatureFlagOff_DoesNotProcess()
    {
        var sutProvider = CreateSutProvider(selfHosted: false, flagEnabled: false);

        await sutProvider.Sut.Execute(Substitute.For<IJobExecutionContext>());

        await sutProvider.GetDependency<IProcessPartnershipReconciliationExportsCommand>().DidNotReceive()
            .ProcessAsync();
    }

    [Fact]
    public async Task Execute_SelfHosted_DoesNotProcess()
    {
        var sutProvider = CreateSutProvider(selfHosted: true, flagEnabled: true);

        await sutProvider.Sut.Execute(Substitute.For<IJobExecutionContext>());

        await sutProvider.GetDependency<IProcessPartnershipReconciliationExportsCommand>().DidNotReceive()
            .ProcessAsync();
    }

    [Fact]
    public async Task Execute_CloudWithFeatureFlagOn_ProcessesOnce()
    {
        var sutProvider = CreateSutProvider(selfHosted: false, flagEnabled: true);
        sutProvider.GetDependency<IProcessPartnershipReconciliationExportsCommand>().ProcessAsync()
            .Returns(new PartnershipReconciliationExportProcessingResult(0, 0, 0));

        await sutProvider.Sut.Execute(Substitute.For<IJobExecutionContext>());

        await sutProvider.GetDependency<IProcessPartnershipReconciliationExportsCommand>().Received(1)
            .ProcessAsync();
    }

    private static SutProvider<ProcessPartnershipReconciliationExportsJob> CreateSutProvider(
        bool selfHosted, bool flagEnabled)
    {
        var sutProvider = new SutProvider<ProcessPartnershipReconciliationExportsJob>()
            .SetDependency(new GlobalSettings { SelfHosted = selfHosted })
            .Create();
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PartnerSponsorships)
            .Returns(flagEnabled);
        return sutProvider;
    }
}
