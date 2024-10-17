using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(OrganizationDomainController))]
[SutProviderCustomize]
public class OrganizationDomainControllerTests
{
    [Theory, BitAutoData]
    public async Task Get_ShouldThrowUnauthorized_WhenOrgIdCannotManageSso(Guid orgId,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(false);

        var requestAction = async () => await sutProvider.Sut.Get(orgId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task Get_ShouldNotFound_WhenOrganizationDoesNotExist(Guid orgId,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.Get(orgId);

        await Assert.ThrowsAsync<NotFoundException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task Get_ShouldReturnOrganizationDomainList_WhenOrgIdIsValid(Guid orgId,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(new Organization());
        sutProvider.GetDependency<IGetOrganizationDomainByOrganizationIdQuery>()
            .GetDomainsByOrganizationIdAsync(orgId).Returns(new List<OrganizationDomain>
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

        var result = await sutProvider.Sut.Get(orgId);

        Assert.IsType<ListResponseModel<OrganizationDomainResponseModel>>(result);
        Assert.Equal(orgId, result.Data.Select(x => x.OrganizationId).FirstOrDefault());
    }

    [Theory, BitAutoData]
    public async Task GetByOrgIdAndId_ShouldThrowUnauthorized_WhenOrgIdCannotManageSso(Guid orgId, Guid id,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(false);

        var requestAction = async () => await sutProvider.Sut.Get(orgId, id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task GetByOrgIdAndId_ShouldThrowNotFound_WhenOrganizationDoesNotExist(Guid orgId, Guid id,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.Get(orgId, id);

        await Assert.ThrowsAsync<NotFoundException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task GetByOrgIdAndId_ShouldThrowNotFound_WhenOrganizationDomainEntryNotExist(Guid orgId, Guid id,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(new Organization());
        sutProvider.GetDependency<IGetOrganizationDomainByIdOrganizationIdQuery>().GetOrganizationDomainByIdOrganizationIdAsync(id, orgId).ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.Get(orgId, id);

        await Assert.ThrowsAsync<NotFoundException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task GetByOrgIdAndId_ShouldThrowNotFound_WhenOrgIdDoesNotMatch(OrganizationDomain organizationDomain,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(organizationDomain.OrganizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationDomain.OrganizationId).Returns(new Organization());
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainByIdOrganizationIdAsync(organizationDomain.Id, organizationDomain.OrganizationId)
            .ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.Get(organizationDomain.OrganizationId, organizationDomain.Id);

        await Assert.ThrowsAsync<NotFoundException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task Get_ShouldReturnOrganizationDomain_WhenOrgIdAndIdAreValid(Guid orgId, Guid id,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).Returns(new Organization());
        sutProvider.GetDependency<IGetOrganizationDomainByIdOrganizationIdQuery>().GetOrganizationDomainByIdOrganizationIdAsync(id, orgId)
            .Returns(new OrganizationDomain
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                CreationDate = DateTime.UtcNow.AddDays(-7),
                DomainName = "test.com",
                Txt = "btw+12342"
            });

        var result = await sutProvider.Sut.Get(orgId, id);

        Assert.IsType<OrganizationDomainResponseModel>(result);
        Assert.Equal(orgId, result.OrganizationId);
    }

    [Theory, BitAutoData]
    public async Task Post_ShouldThrowUnauthorized_OrgIdCannotManageSso(Guid orgId, OrganizationDomainRequestModel model,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(false);

        var requestAction = async () => await sutProvider.Sut.Post(orgId, model);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task Post_ShouldThrowNotFound_WhenOrganizationDoesNotExist(Guid orgId, OrganizationDomainRequestModel model,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.Post(orgId, model);

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

        var result = await sutProvider.Sut.Post(orgId, model);

        await sutProvider.GetDependency<ICreateOrganizationDomainCommand>().ReceivedWithAnyArgs(1)
            .CreateAsync(Arg.Any<OrganizationDomain>());
        Assert.IsType<OrganizationDomainResponseModel>(result);
    }

    [Theory, BitAutoData]
    public async Task Verify_ShouldThrowUnauthorized_WhenOrgIdCannotManageSso(Guid orgId, Guid id,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(false);

        var requestAction = async () => await sutProvider.Sut.Verify(orgId, id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task Verify_ShouldThrowNotFound_WhenOrganizationDoesNotExist(Guid orgId, Guid id,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.Verify(orgId, id);

        await Assert.ThrowsAsync<NotFoundException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task VerifyOrganizationDomain_ShouldThrowNotFound_WhenOrgIdDoesNotMatch(OrganizationDomain organizationDomain,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(organizationDomain.OrganizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationDomain.OrganizationId).Returns(new Organization());
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainByIdOrganizationIdAsync(organizationDomain.Id, organizationDomain.OrganizationId)
            .ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.Verify(organizationDomain.OrganizationId, organizationDomain.Id);

        await Assert.ThrowsAsync<NotFoundException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task Verify_WhenRequestIsValid(OrganizationDomain organizationDomain,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(organizationDomain.OrganizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationDomain.OrganizationId).Returns(new Organization());
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainByIdOrganizationIdAsync(organizationDomain.Id, organizationDomain.OrganizationId)
            .Returns(organizationDomain);
        sutProvider.GetDependency<IVerifyOrganizationDomainCommand>().UserVerifyOrganizationDomainAsync(organizationDomain)
            .Returns(new OrganizationDomain());

        var result = await sutProvider.Sut.Verify(organizationDomain.OrganizationId, organizationDomain.Id);

        await sutProvider.GetDependency<IVerifyOrganizationDomainCommand>().Received(1)
            .UserVerifyOrganizationDomainAsync(organizationDomain);
        Assert.IsType<OrganizationDomainResponseModel>(result);
    }

    [Theory, BitAutoData]
    public async Task RemoveDomain_ShouldThrowUnauthorized_OrgIdCannotManageSso(Guid orgId, Guid id,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(false);

        var requestAction = async () => await sutProvider.Sut.RemoveDomain(orgId, id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task RemoveDomain_ShouldThrowNotFound_WhenOrganizationDoesNotExist(Guid orgId, Guid id,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(orgId).ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.RemoveDomain(orgId, id);

        await Assert.ThrowsAsync<NotFoundException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task RemoveDomain_ShouldThrowNotFound_WhenOrgIdDoesNotMatch(OrganizationDomain organizationDomain,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(organizationDomain.OrganizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationDomain.OrganizationId).Returns(new Organization());
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainByIdOrganizationIdAsync(organizationDomain.Id, organizationDomain.OrganizationId)
            .ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.RemoveDomain(organizationDomain.OrganizationId, organizationDomain.Id);

        await Assert.ThrowsAsync<NotFoundException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task RemoveDomain_WhenRequestIsValid(OrganizationDomain organizationDomain,
        SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageSso(organizationDomain.OrganizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationDomain.OrganizationId).Returns(new Organization());
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetDomainByIdOrganizationIdAsync(organizationDomain.Id, organizationDomain.OrganizationId)
            .Returns(organizationDomain);

        await sutProvider.Sut.RemoveDomain(organizationDomain.OrganizationId, organizationDomain.Id);

        await sutProvider.GetDependency<IDeleteOrganizationDomainCommand>().Received(1)
            .DeleteAsync(organizationDomain);
    }

    [Theory, BitAutoData]
    public async Task GetOrgDomainSsoDetails_ShouldThrowNotFound_WhenEmailHasNotClaimedDomain(
        OrganizationDomainSsoDetailsRequestModel model, SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetOrganizationDomainSsoDetailsAsync(model.Email).ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.GetOrgDomainSsoDetails(model);

        await Assert.ThrowsAsync<NotFoundException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task GetOrgDomainSsoDetails_ShouldReturnOrganizationDomainSsoDetails_WhenEmailHasClaimedDomain(
        OrganizationDomainSsoDetailsRequestModel model, OrganizationDomainSsoDetailsData ssoDetailsData, SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetOrganizationDomainSsoDetailsAsync(model.Email).Returns(ssoDetailsData);

        var result = await sutProvider.Sut.GetOrgDomainSsoDetails(model);

        Assert.IsType<OrganizationDomainSsoDetailsResponseModel>(result);
    }

    [Theory, BitAutoData]
    public async Task GetVerifiedOrgDomainSsoDetails_ShouldThrowNotFound_WhenEmailHasNotClaimedDomain(
        OrganizationDomainSsoDetailsRequestModel model, SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetVerifiedOrganizationDomainSsoDetailsAsync(model.Email).Returns(Array.Empty<VerifiedOrganizationDomainSsoDetail>());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetOrgDomainSsoDetails(model));
    }

    [Theory, BitAutoData]
    public async Task GetVerifiedOrgDomainSsoDetails_ShouldReturnOrganizationDomainSsoDetails_WhenEmailHasClaimedDomain(
        OrganizationDomainSsoDetailsRequestModel model, IEnumerable<VerifiedOrganizationDomainSsoDetail> ssoDetailsData, SutProvider<OrganizationDomainController> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationDomainRepository>()
            .GetVerifiedOrganizationDomainSsoDetailsAsync(model.Email).Returns(ssoDetailsData);

        var result = await sutProvider.Sut.GetVerifiedOrgDomainSsoDetailsAsync(model);

        Assert.IsType<VerifiedOrganizationDomainSsoDetailsResponseModel>(result);
    }
}
