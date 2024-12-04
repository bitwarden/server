using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationDomains;

[SutProviderCustomize]
public class VerifyOrganizationDomainCommandTests
{
    [Theory, BitAutoData]
    public async Task UserVerifyOrganizationDomainAsync_ShouldThrowConflict_WhenDomainHasBeenClaimed(Guid id,
        SutProvider<VerifyOrganizationDomainCommand> sutProvider)
    {
        var expected = new OrganizationDomain
        {
            Id = id,
            OrganizationId = Guid.NewGuid(),
            DomainName = "Test Domain",
            Txt = "btw+test18383838383"
        };

        sutProvider.GetDependency<ICurrentContext>()
            .UserId.Returns(Guid.NewGuid());

        expected.SetVerifiedDate();

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetByIdAsync(id)
            .Returns(expected);

        var requestAction = async () => await sutProvider.Sut.UserVerifyOrganizationDomainAsync(expected);

        var exception = await Assert.ThrowsAsync<ConflictException>(requestAction);
        Assert.Contains("Domain has already been verified.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UserVerifyOrganizationDomainAsync_ShouldThrowConflict_WhenDomainHasBeenClaimedByAnotherOrganization(Guid id,
        SutProvider<VerifyOrganizationDomainCommand> sutProvider)
    {
        var expected = new OrganizationDomain
        {
            Id = id,
            OrganizationId = Guid.NewGuid(),
            DomainName = "Test Domain",
            Txt = "btw+test18383838383"
        };
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetByIdAsync(id)
            .Returns(expected);

        sutProvider.GetDependency<ICurrentContext>()
            .UserId.Returns(Guid.NewGuid());

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetClaimedDomainsByDomainNameAsync(expected.DomainName)
            .Returns(new List<OrganizationDomain> { expected });

        var requestAction = async () => await sutProvider.Sut.UserVerifyOrganizationDomainAsync(expected);

        var exception = await Assert.ThrowsAsync<ConflictException>(requestAction);
        Assert.Contains("The domain is not available to be claimed.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UserVerifyOrganizationDomainAsync_ShouldVerifyDomainUpdateAndLogEvent_WhenTxtRecordExists(Guid id,
        SutProvider<VerifyOrganizationDomainCommand> sutProvider)
    {
        var expected = new OrganizationDomain
        {
            Id = id,
            OrganizationId = Guid.NewGuid(),
            DomainName = "Test Domain",
            Txt = "btw+test18383838383"
        };
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetByIdAsync(id)
            .Returns(expected);

        sutProvider.GetDependency<ICurrentContext>()
            .UserId.Returns(Guid.NewGuid());

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetClaimedDomainsByDomainNameAsync(expected.DomainName)
            .Returns(new List<OrganizationDomain>());

        sutProvider.GetDependency<IDnsResolverService>()
            .ResolveAsync(expected.DomainName, Arg.Any<string>())
            .Returns(true);

        var result = await sutProvider.Sut.UserVerifyOrganizationDomainAsync(expected);

        Assert.NotNull(result.VerifiedDate);
        await sutProvider.GetDependency<IOrganizationDomainRepository>().Received(1)
            .ReplaceAsync(Arg.Any<OrganizationDomain>());
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationDomainEventAsync(Arg.Any<OrganizationDomain>(), EventType.OrganizationDomain_Verified);
    }

    [Theory, BitAutoData]
    public async Task UserVerifyOrganizationDomainAsync_ShouldNotSetVerifiedDate_WhenTxtRecordDoesNotExist(Guid id,
        SutProvider<VerifyOrganizationDomainCommand> sutProvider)
    {
        var expected = new OrganizationDomain
        {
            Id = id,
            OrganizationId = Guid.NewGuid(),
            DomainName = "Test Domain",
            Txt = "btw+test18383838383"
        };
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetByIdAsync(id)
            .Returns(expected);

        sutProvider.GetDependency<ICurrentContext>()
            .UserId.Returns(Guid.NewGuid());

        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetClaimedDomainsByDomainNameAsync(expected.DomainName)
            .Returns(new List<OrganizationDomain>());

        sutProvider.GetDependency<IDnsResolverService>()
            .ResolveAsync(expected.DomainName, Arg.Any<string>())
            .Returns(false);

        var result = await sutProvider.Sut.UserVerifyOrganizationDomainAsync(expected);

        Assert.Null(result.VerifiedDate);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationDomainEventAsync(Arg.Any<OrganizationDomain>(), EventType.OrganizationDomain_NotVerified);
    }


    [Theory, BitAutoData]
    public async Task SystemVerifyOrganizationDomainAsync_CallsEventServiceWithUpdatedJobRunCount(SutProvider<VerifyOrganizationDomainCommand> sutProvider)
    {
        var domain = new OrganizationDomain()
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            CreationDate = DateTime.UtcNow,
            DomainName = "test.com",
            Txt = "btw+12345",
        };

        _ = await sutProvider.Sut.SystemVerifyOrganizationDomainAsync(domain);

        await sutProvider.GetDependency<IEventService>().ReceivedWithAnyArgs(1)
            .LogOrganizationDomainEventAsync(default, EventType.OrganizationDomain_NotVerified,
                EventSystemUser.DomainVerification);
    }

    [Theory, BitAutoData]
    public async Task UserVerifyOrganizationDomainAsync_GivenOrganizationDomainWithAccountDeprovisioningEnabled_WhenDomainIsVerified_ThenSingleOrgPolicyShouldBeEnabled(
        OrganizationDomain domain, Guid userId, SutProvider<VerifyOrganizationDomainCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetClaimedDomainsByDomainNameAsync(domain.DomainName)
            .Returns([]);

        sutProvider.GetDependency<IDnsResolverService>()
            .ResolveAsync(domain.DomainName, domain.Txt)
            .Returns(true);

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .UserId.Returns(userId);

        _ = await sutProvider.Sut.UserVerifyOrganizationDomainAsync(domain);

        await sutProvider.GetDependency<ISavePolicyCommand>()
            .Received(1)
            .SaveAsync(Arg.Is<PolicyUpdate>(x => x.Type == PolicyType.SingleOrg &&
                x.OrganizationId == domain.OrganizationId &&
                x.Enabled &&
                x.PerformedBy is StandardUser &&
                x.PerformedBy.UserId == userId));
    }

    [Theory, BitAutoData]
    public async Task UserVerifyOrganizationDomainAsync_GivenOrganizationDomainWithAccountDeprovisioningDisabled_WhenDomainIsVerified_ThenSingleOrgPolicyShouldBeNotBeEnabled(
        OrganizationDomain domain, SutProvider<VerifyOrganizationDomainCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetClaimedDomainsByDomainNameAsync(domain.DomainName)
            .Returns([]);

        sutProvider.GetDependency<IDnsResolverService>()
            .ResolveAsync(domain.DomainName, domain.Txt)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>()
            .UserId.Returns(Guid.NewGuid());

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            .Returns(false);

        _ = await sutProvider.Sut.UserVerifyOrganizationDomainAsync(domain);

        await sutProvider.GetDependency<ISavePolicyCommand>()
            .DidNotReceive()
            .SaveAsync(Arg.Any<PolicyUpdate>());
    }

    [Theory, BitAutoData]
    public async Task UserVerifyOrganizationDomainAsync_GivenOrganizationDomainWithAccountDeprovisioningEnabled_WhenDomainIsNotVerified_ThenSingleOrgPolicyShouldNotBeEnabled(
        OrganizationDomain domain, SutProvider<VerifyOrganizationDomainCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetClaimedDomainsByDomainNameAsync(domain.DomainName)
            .Returns([]);

        sutProvider.GetDependency<IDnsResolverService>()
            .ResolveAsync(domain.DomainName, domain.Txt)
            .Returns(false);

        sutProvider.GetDependency<ICurrentContext>()
            .UserId.Returns(Guid.NewGuid());

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            .Returns(true);

        _ = await sutProvider.Sut.UserVerifyOrganizationDomainAsync(domain);

        await sutProvider.GetDependency<ISavePolicyCommand>()
            .DidNotReceive()
            .SaveAsync(Arg.Any<PolicyUpdate>());
    }

    [Theory, BitAutoData]
    public async Task UserVerifyOrganizationDomainAsync_GivenOrganizationDomainWithAccountDeprovisioningDisabled_WhenDomainIsNotVerified_ThenSingleOrgPolicyShouldBeNotBeEnabled(
        OrganizationDomain domain, SutProvider<VerifyOrganizationDomainCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetClaimedDomainsByDomainNameAsync(domain.DomainName)
            .Returns([]);

        sutProvider.GetDependency<IDnsResolverService>()
            .ResolveAsync(domain.DomainName, domain.Txt)
            .Returns(false);

        sutProvider.GetDependency<ICurrentContext>()
            .UserId.Returns(Guid.NewGuid());

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            .Returns(true);

        _ = await sutProvider.Sut.UserVerifyOrganizationDomainAsync(domain);

        await sutProvider.GetDependency<ISavePolicyCommand>()
            .DidNotReceive()
            .SaveAsync(Arg.Any<PolicyUpdate>());
    }
}
