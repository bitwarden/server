using Bit.Api.Controllers;
using Bit.Api.Models.Request;
using Bit.Api.Models.Response;
using Bit.Api.Models.Response.Organizations;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;
using Organization = Bit.Core.Entities.Organization;
using OrganizationDomain = Bit.Core.Entities.OrganizationDomain;

namespace Bit.Api.Test.Controllers;

[ControllerCustomize(typeof(OrganizationDomainController))]
[SutProviderCustomize]
public class OrganizationDomainControllerTests
{
    [Theory, BitAutoData]
    public async Task Get_ShouldThrowUnauthorized_WhenOrgIdCannotManageSso(Guid orgId,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(false);

        var requestAction = async () => await sutProvider.Sut.Get(orgId.ToString());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task Get_ShouldNotFound_WhenOrganizationDoesNotExist(Guid orgId,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.Get(orgId.ToString());

        await Assert.ThrowsAsync<NotFoundException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task Get_ShouldReturnOrganizationDomainList_WhenOrgIdIsValid(Guid orgId,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(new Organization());
        sutProvider.GetDependency<IGetOrganizationDomainByOrganizationIdQuery>()
            .GetDomainsByOrganizationId(orgId).Returns(new List<OrganizationDomain>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId,
                    CreationDate = DateTime.UtcNow.AddDays(-7),
                    DomainName = "test.com",
                    Txt = "btw+12342"
                }
            });

        var result = await sutProvider.Sut.Get(orgId.ToString());

        Assert.IsType<ListResponseModel<OrganizationDomainResponseModel>>(result);
        Assert.Equal(orgId.ToString(), result.Data.Select(x => x.OrganizationId).FirstOrDefault());
    }

    [Theory, BitAutoData]
    public async Task GetByOrgIdAndId_ShouldThrowUnauthorized_WhenOrgIdCannotManageSso(Guid orgId, Guid id,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(false);

        var requestAction = async () => await sutProvider.Sut.Get(orgId.ToString(), id.ToString());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task GetByOrgIdAndId_ShouldThrowNotFound_WhenOrganizationDoesNotExist(Guid orgId, Guid id,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.Get(orgId.ToString(), id.ToString());

        await Assert.ThrowsAsync<NotFoundException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task GetByOrgIdAndId_ShouldThrowNotFound_WhenOrganizationDomainEntryNotExist(Guid orgId, Guid id,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(new Organization());
        sutProvider.GetDependency<IGetOrganizationDomainByIdQuery>().GetOrganizationDomainById(id).ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.Get(orgId.ToString(), id.ToString());

        await Assert.ThrowsAsync<NotFoundException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task Get_ShouldReturnOrganizationDomain_WhenOrgIdAndIdAreValid(Guid orgId, Guid id,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(new Organization());
        sutProvider.GetDependency<IGetOrganizationDomainByIdQuery>().GetOrganizationDomainById(id)
            .Returns(new OrganizationDomain{
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                CreationDate = DateTime.UtcNow.AddDays(-7),
                DomainName = "test.com",
                Txt = "btw+12342"
            });

        var result = await sutProvider.Sut.Get(orgId.ToString(), id.ToString());

        Assert.IsType<OrganizationDomainResponseModel>(result);
        Assert.Equal(orgId.ToString(), result.OrganizationId);
    }

    [Theory, BitAutoData]
    public async Task Post_ShouldThrowUnauthorized_OrgIdCannotManageSso(Guid orgId, OrganizationDomainRequestModel model,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(false);

        var requestAction = async () => await sutProvider.Sut.Post(orgId.ToString(), model);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task Post_ShouldThrowNotFound_WhenOrganizationDoesNotExist(Guid orgId, OrganizationDomainRequestModel model,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.Post(orgId.ToString(), model);

        await Assert.ThrowsAsync<NotFoundException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task Post_ShouldCreateEntry_WhenRequestIsValid(Guid orgId, OrganizationDomainRequestModel model,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(new Organization());
        sutProvider.GetDependency<ICreateOrganizationDomainCommand>().CreateAsync(Arg.Any<OrganizationDomain>())
            .Returns(new OrganizationDomain());

        var result = await sutProvider.Sut.Post(orgId.ToString(), model);

        await sutProvider.GetDependency<ICreateOrganizationDomainCommand>().ReceivedWithAnyArgs(1)
            .CreateAsync(Arg.Any<OrganizationDomain>());
        Assert.IsType<OrganizationDomainResponseModel>(result);
    }

    [Theory, BitAutoData]
    public async Task Verify_ShouldThrowUnauthorized_WhenOrgIdCannotManageSso(Guid orgId, Guid id,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(false);

        var requestAction = async () => await sutProvider.Sut.Verify(orgId.ToString(), id.ToString());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task Verify_ShouldThrowNotFound_WhenOrganizationDoesNotExist(Guid orgId, Guid id,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.Verify(orgId.ToString(), id.ToString());

        await Assert.ThrowsAsync<NotFoundException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task Verify_WhenRequestIsValid(Guid orgId, Guid id,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(new Organization());
        sutProvider.GetDependency<IVerifyOrganizationDomainCommand>().VerifyOrganizationDomain(id)
            .Returns(new OrganizationDomain());

        var result = await sutProvider.Sut.Verify(orgId.ToString(), id.ToString());

        await sutProvider.GetDependency<IVerifyOrganizationDomainCommand>().Received(1)
            .VerifyOrganizationDomain(id);
        Assert.IsType<OrganizationDomainResponseModel>(result);
    }

    [Theory, BitAutoData]
    public async Task RemoveDomain_ShouldThrowUnauthorized_OrgIdCannotManageSso(Guid orgId, Guid id,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(false);

        var requestAction = async () => await sutProvider.Sut.RemoveDomain(orgId.ToString(), id.ToString());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task RemoveDomain_ShouldThrowNotFound_WhenOrganizationDoesNotExist(Guid orgId, Guid id,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.RemoveDomain(orgId.ToString(), id.ToString());

        await Assert.ThrowsAsync<NotFoundException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task RemoveDomain_WhenRequestIsValid(Guid orgId, Guid id,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(new Organization());

        await sutProvider.Sut.RemoveDomain(orgId.ToString(), id.ToString());

        await sutProvider.GetDependency<IDeleteOrganizationDomainCommand>().Received(1)
            .DeleteAsync(id);
    }
}
