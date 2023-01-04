using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationDomains;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationDomains;

[SutProviderCustomize]
public class CreateOrganizationDomainCommandTests
{
    [Theory, BitAutoData]
    public async Task CreateAsync_ShouldCreateOrganizationDomainAndLogEvent_WhenDetailsAreValid(OrganizationDomain orgDomain, SutProvider<CreateOrganizationDomainCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetClaimedDomainsByDomainNameAsync(orgDomain.DomainName)
            .Returns(new List<OrganizationDomain>());
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainByOrgIdAndDomainNameAsync(orgDomain.OrganizationId, orgDomain.DomainName)
            .ReturnsNull();
        sutProvider.GetDependency<IDnsResolverService>()
            .ResolveAsync(orgDomain.DomainName, orgDomain.Txt)
            .Returns(false);
        orgDomain.SetNextRunDate(12);
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .CreateAsync(orgDomain)
            .Returns(orgDomain);


        var result = await sutProvider.Sut.CreateAsync(orgDomain);

        Assert.Equal(orgDomain.Id, result.Id);
        Assert.Equal(orgDomain.OrganizationId, result.OrganizationId);
        Assert.NotNull(result.LastCheckedDate);
        Assert.Equal(orgDomain.NextRunDate, result.NextRunDate);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationDomainEventAsync(Arg.Any<OrganizationDomain>(), EventType.OrganizationDomain_Added);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationDomainEventAsync(Arg.Any<OrganizationDomain>(), Arg.Is<EventType>(x => x == EventType.OrganizationDomain_NotVerified));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_ShouldThrowConflictException_WhenDomainIsClaimed(OrganizationDomain orgDomain,
        SutProvider<CreateOrganizationDomainCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetClaimedDomainsByDomainNameAsync(orgDomain.DomainName)
            .Returns(new List<OrganizationDomain>()
            {
                orgDomain
            });

        var requestAction = async () => await sutProvider.Sut.CreateAsync(orgDomain);

        var exception = await Assert.ThrowsAsync<ConflictException>(requestAction);
        Assert.Contains("The domain is not available to be claimed.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_ShouldThrowConflictException_WhenEntryIsDuplicatedForOrganization(OrganizationDomain orgDomain,
        SutProvider<CreateOrganizationDomainCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetClaimedDomainsByDomainNameAsync(orgDomain.DomainName)
            .Returns(new List<OrganizationDomain>());
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainByOrgIdAndDomainNameAsync(orgDomain.OrganizationId, orgDomain.DomainName)
            .Returns(orgDomain);

        var requestAction = async () => await sutProvider.Sut.CreateAsync(orgDomain);

        var exception = await Assert.ThrowsAsync<ConflictException>(requestAction);
        Assert.Contains("A domain already exists for this organization.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_ShouldNotSetVerifiedDate_WhenDomainCannotBeResolved(OrganizationDomain orgDomain,
        SutProvider<CreateOrganizationDomainCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetClaimedDomainsByDomainNameAsync(orgDomain.DomainName)
            .Returns(new List<OrganizationDomain>());
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainByOrgIdAndDomainNameAsync(orgDomain.OrganizationId, orgDomain.DomainName)
            .ReturnsNull();
        sutProvider.GetDependency<IDnsResolverService>()
            .ResolveAsync(orgDomain.DomainName, orgDomain.Txt)
            .Throws(new TxtRecordNotFoundException());
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .CreateAsync(orgDomain)
            .Returns(orgDomain);

        await sutProvider.Sut.CreateAsync(orgDomain);

        Assert.Null(orgDomain.VerifiedDate);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_ShouldSetVerifiedDateAndLogEvent_WhenDomainIsResolved(OrganizationDomain orgDomain,
        SutProvider<CreateOrganizationDomainCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetClaimedDomainsByDomainNameAsync(orgDomain.DomainName)
            .Returns(new List<OrganizationDomain>());
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainByOrgIdAndDomainNameAsync(orgDomain.OrganizationId, orgDomain.DomainName)
            .ReturnsNull();
        sutProvider.GetDependency<IDnsResolverService>()
            .ResolveAsync(orgDomain.DomainName, orgDomain.Txt)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .CreateAsync(orgDomain)
            .Returns(orgDomain);

        var result = await sutProvider.Sut.CreateAsync(orgDomain);

        Assert.NotNull(result.VerifiedDate);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationDomainEventAsync(Arg.Any<OrganizationDomain>(), EventType.OrganizationDomain_Added);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationDomainEventAsync(Arg.Any<OrganizationDomain>(), Arg.Is<EventType>(x => x == EventType.OrganizationDomain_Verified));
    }
}
