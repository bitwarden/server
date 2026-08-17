using System.Security.Claims;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Endpoints;

[SutProviderCustomize]
public class CipherLeaseEndpointsHandlerTests
{
    private static readonly ClaimsPrincipal _user = new();

    [Theory, BitAutoData]
    public async Task State_ReturnsSnapshotFromQuery(
        Guid id, Guid userId, Bit.Pam.Entities.AccessLease activeLease, SutProvider<CipherLeaseEndpointsHandler> sutProvider)
    {
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);
        sutProvider.GetDependency<IGetCipherAccessStateQuery>()
            .GetStateAsync(userId, id)
            .Returns(new Bit.Services.Pam.Models.CipherAccessState(id, activeLease, null, null));

        var result = await sutProvider.Sut.State(_user, id);

        Assert.Equal(id, result.CipherId);
        Assert.NotNull(result.ActiveLease);
        Assert.Equal(activeLease.Id, result.ActiveLease!.Id);
        Assert.Null(result.PendingRequest);
        Assert.Null(result.ApprovedRequest);
    }
}
