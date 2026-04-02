using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request.Providers;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(ProviderOrganizationsController))]
[SutProviderCustomize]
public class ProviderOrganizationsControllerTests
{
    [Theory, BitAutoData]
    public async Task Add_NotProviderAdmin_ThrowsNotFound(
        Guid providerId,
        ProviderOrganizationAddRequestModel model,
        SutProvider<ProviderOrganizationsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageProviderOrganizations(providerId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Add(providerId, model));

        await sutProvider.GetDependency<IProviderService>().DidNotReceiveWithAnyArgs()
            .AddOrganization(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task Add_NotOrgOwner_ThrowsNotFound(
        Guid providerId,
        ProviderOrganizationAddRequestModel model,
        SutProvider<ProviderOrganizationsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageProviderOrganizations(providerId)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.OrganizationId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Add(providerId, model));

        await sutProvider.GetDependency<IProviderService>().DidNotReceiveWithAnyArgs()
            .AddOrganization(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task Add_Ok(
        Guid providerId,
        ProviderOrganizationAddRequestModel model,
        SutProvider<ProviderOrganizationsController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageProviderOrganizations(providerId)
            .Returns(true);

        sutProvider.GetDependency<ICurrentContext>().OrganizationOwner(model.OrganizationId)
            .Returns(true);

        await sutProvider.Sut.Add(providerId, model);

        await sutProvider.GetDependency<IProviderService>().Received(1)
            .AddOrganization(providerId, model.OrganizationId, model.Key);
    }
}
