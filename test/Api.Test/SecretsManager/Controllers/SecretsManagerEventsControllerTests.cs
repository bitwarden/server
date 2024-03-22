using System.Security.Claims;
using Bit.Api.SecretsManager.Controllers;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.SecretsManager.Controllers;

[ControllerCustomize(typeof(SecretsManagerEventsController))]
[SutProviderCustomize]
[JsonDocumentCustomize]
public class SecretsManagerEventsControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task GetServiceAccountEvents_NoAccess_Throws(SutProvider<SecretsManagerEventsController> sutProvider,
        ServiceAccount data)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Failed());


        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetServiceAccountEventsAsync(data.Id));
        await sutProvider.GetDependency<IEventRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationServiceAccountAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(),
                Arg.Any<DateTime>(), Arg.Any<PageOptions>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetServiceAccountEvents_DateRangeOver_Throws(
        SutProvider<SecretsManagerEventsController> sutProvider,
        ServiceAccount data)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());

        var start = DateTime.UtcNow.AddYears(-1);
        var end = DateTime.UtcNow.AddYears(1);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.GetServiceAccountEventsAsync(data.Id, start, end));

        await sutProvider.GetDependency<IEventRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationServiceAccountAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(),
                Arg.Any<DateTime>(), Arg.Any<PageOptions>());
    }

    [Theory]
    [BitAutoData]
    public async Task GetServiceAccountEvents_Success(SutProvider<SecretsManagerEventsController> sutProvider,
        ServiceAccount data)
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(default).ReturnsForAnyArgs(data);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), data,
                Arg.Any<IEnumerable<IAuthorizationRequirement>>()).ReturnsForAnyArgs(AuthorizationResult.Success());
        sutProvider.GetDependency<IEventRepository>()
            .GetManyByOrganizationServiceAccountAsync(default, default, default, default, default)
            .ReturnsForAnyArgs(new PagedResult<IEvent>());

        await sutProvider.Sut.GetServiceAccountEventsAsync(data.Id);

        await sutProvider.GetDependency<IEventRepository>().Received(1)
            .GetManyByOrganizationServiceAccountAsync(data.OrganizationId, data.Id, Arg.Any<DateTime>(),
                Arg.Any<DateTime>(), Arg.Any<PageOptions>());
    }
}
