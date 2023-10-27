using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;
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
    public async Task VerifyOrganizationDomain_ShouldThrowConflict_WhenDomainHasBeenClaimed(Guid id,
        SutProvider<VerifyOrganizationDomainCommand> sutProvider)
    {
        var expected = new OrganizationDomain
        {
            Id = id,
            OrganizationId = Guid.NewGuid(),
            DomainName = "Test Domain",
            Txt = "btw+test18383838383"
        };
        expected.SetVerifiedDate();
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetByIdAsync(id)
            .Returns(expected);

        var requestAction = async () => await sutProvider.Sut.VerifyOrganizationDomainAsync(expected);

        var exception = await Assert.ThrowsAsync<ConflictException>(requestAction);
        Assert.Contains("Domain has already been verified.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task VerifyOrganizationDomain_ShouldThrowConflict_WhenDomainHasBeenClaimedByAnotherOrganization(Guid id,
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
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetClaimedDomainsByDomainNameAsync(expected.DomainName)
            .Returns(new List<OrganizationDomain> { expected });

        var requestAction = async () => await sutProvider.Sut.VerifyOrganizationDomainAsync(expected);

        var exception = await Assert.ThrowsAsync<ConflictException>(requestAction);
        Assert.Contains("The domain is not available to be claimed.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task VerifyOrganizationDomain_ShouldVerifyDomainUpdateAndLogEvent_WhenTxtRecordExists(Guid id,
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
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetClaimedDomainsByDomainNameAsync(expected.DomainName)
            .Returns(new List<OrganizationDomain>());
        sutProvider.GetDependency<IDnsResolverService>()
            .ResolveAsync(expected.DomainName, Arg.Any<string>())
            .Returns(true);

        var result = await sutProvider.Sut.VerifyOrganizationDomainAsync(expected);

        Assert.NotNull(result.VerifiedDate);
        await sutProvider.GetDependency<IOrganizationDomainRepository>().Received(1)
            .ReplaceAsync(Arg.Any<OrganizationDomain>());
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationDomainEventAsync(Arg.Any<OrganizationDomain>(), EventType.OrganizationDomain_Verified);
    }

    [Theory, BitAutoData]
    public async Task VerifyOrganizationDomain_ShouldNotSetVerifiedDate_WhenTxtRecordDoesNotExist(Guid id,
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
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetClaimedDomainsByDomainNameAsync(expected.DomainName)
            .Returns(new List<OrganizationDomain>());
        sutProvider.GetDependency<IDnsResolverService>()
            .ResolveAsync(expected.DomainName, Arg.Any<string>())
            .Returns(false);

        var result = await sutProvider.Sut.VerifyOrganizationDomainAsync(expected);

        Assert.Null(result.VerifiedDate);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationDomainEventAsync(Arg.Any<OrganizationDomain>(), EventType.OrganizationDomain_NotVerified);
    }
}
