using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationDomains;

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
        orgDomain.SetNextRunDate(12);
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .CreateAsync(orgDomain)
            .Returns(orgDomain);


        var result = await sutProvider.Sut.CreateAsync(orgDomain);

        Assert.Equal(orgDomain.Id, result.Id);
        Assert.Equal(orgDomain.OrganizationId, result.OrganizationId);
        Assert.Null(result.LastCheckedDate);
        Assert.Equal(orgDomain.Txt, result.Txt);
        Assert.Equal(orgDomain.Txt.Length == 47, result.Txt.Length == 47);
        Assert.Equal(orgDomain.NextRunDate, result.NextRunDate);
        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationDomainEventAsync(Arg.Any<OrganizationDomain>(), EventType.OrganizationDomain_Added);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_ShouldGenerateSecureTxtToken(OrganizationDomain orgDomain, SutProvider<CreateOrganizationDomainCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetClaimedDomainsByDomainNameAsync(orgDomain.DomainName)
            .Returns(new List<OrganizationDomain>());
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainByOrgIdAndDomainNameAsync(orgDomain.OrganizationId, orgDomain.DomainName)
            .ReturnsNull();
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .CreateAsync(Arg.Any<OrganizationDomain>())
            .Returns(callInfo => callInfo.Arg<OrganizationDomain>());

        var first = await sutProvider.Sut.CreateAsync(orgDomain);
        var firstToken = first.Txt;

        var second = await sutProvider.Sut.CreateAsync(orgDomain);

        Assert.StartsWith("bw=", firstToken);
        var value = firstToken["bw=".Length..];
        Assert.Equal(44, value.Length);
        Assert.All(value, c => Assert.True(char.IsLetterOrDigit(c)));
        Assert.NotEqual(firstToken, second.Txt);
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
        Assert.Contains(new DuplicateDomainError().Message, exception.Message);
    }
}
