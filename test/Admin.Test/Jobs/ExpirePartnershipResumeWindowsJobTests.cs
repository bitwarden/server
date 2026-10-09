using Bit.Admin.Jobs;
using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Test.Common.AutoFixture;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using Quartz;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Admin.Test.Jobs;

public class ExpirePartnershipResumeWindowsJobTests
{
    [Fact]
    public async Task Execute_FeatureFlagOff_DoesNotExpire()
    {
        var sutProvider = CreateSutProvider(selfHosted: false, flagEnabled: false);

        await sutProvider.Sut.Execute(Substitute.For<IJobExecutionContext>());

        await sutProvider.GetDependency<IExpirePartnershipResumeWindowsCommand>().DidNotReceive().ExpireAsync();
    }

    [Fact]
    public async Task Execute_SelfHosted_DoesNotExpire()
    {
        var sutProvider = CreateSutProvider(selfHosted: true, flagEnabled: true);

        await sutProvider.Sut.Execute(Substitute.For<IJobExecutionContext>());

        await sutProvider.GetDependency<IExpirePartnershipResumeWindowsCommand>().DidNotReceive().ExpireAsync();
    }

    [Fact]
    public async Task Execute_CloudWithFeatureFlagOn_ExpiresOnce()
    {
        var sutProvider = CreateSutProvider(selfHosted: false, flagEnabled: true);
        sutProvider.GetDependency<IExpirePartnershipResumeWindowsCommand>().ExpireAsync().Returns(0);

        await sutProvider.Sut.Execute(Substitute.For<IJobExecutionContext>());

        await sutProvider.GetDependency<IExpirePartnershipResumeWindowsCommand>().Received(1).ExpireAsync();
    }

    private static SutProvider<ExpirePartnershipResumeWindowsJob> CreateSutProvider(bool selfHosted, bool flagEnabled)
    {
        var sutProvider = new SutProvider<ExpirePartnershipResumeWindowsJob>()
            .SetDependency(new GlobalSettings { SelfHosted = selfHosted })
            .Create();
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.PartnerSponsorships).Returns(flagEnabled);
        return sutProvider;
    }
}
