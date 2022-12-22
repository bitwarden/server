using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationDomains;
using Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationDomains;

[SutProviderCustomize]
public class VerifyOrganizationDomainCommandTests
{
    [Theory, BitAutoData]
    public async Task VerifyOrganizationDomain_ShouldThrowNotFound_WhenDomainDoesNotExist(Guid id,
        SutProvider<VerifyOrganizationDomainCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetByIdAsync(id)
            .ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.VerifyOrganizationDomain(id);

        await Assert.ThrowsAsync<NotFoundException>(requestAction);
    }
    
    [Theory, BitAutoData]
    public async Task VerifyOrganizationDomain_ShouldThrowConflict_WhenDomainHasbeenClaimed(Guid id,
        SutProvider<VerifyOrganizationDomainCommand> sutProvider)
    {
        var expected = new OrganizationDomain
        {
            Id = id, OrganizationId = Guid.NewGuid(), DomainName = "Test Domain", Txt = "btw+test18383838383"
        };
        expected.SetVerifiedDate();
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetByIdAsync(id)
            .Returns(expected);

        var requestAction = async () => await sutProvider.Sut.VerifyOrganizationDomain(id);

        var exception = await Assert.ThrowsAsync<ConflictException>(requestAction);
        Assert.Contains("Domain has already been verified.", exception.Message);
    }
}
