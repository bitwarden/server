using AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Polly;
using Polly.Registry;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Helpers;

public class ResiliencePipelineProviderCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new ResiliencePipelineProviderCustomization();
}

public class ResiliencePipelineProviderCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        var provider = Substitute.For<ResiliencePipelineProvider<string>>();
        provider.GetPipeline(Arg.Any<string>()).Returns(ResiliencePipeline.Empty);
        fixture.Register(() => provider);
    }
}
