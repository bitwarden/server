using System.Security.Claims;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Vault.Authorization.SecurityTasks;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Queries;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.Queries;

[SutProviderCustomize]
public class GetTasksForOrganizationQueryTests
{
    [Theory, BitAutoData]
    public async Task GetTasksAsync_Success(
        Guid userId, CurrentContextOrganization org,
        SutProvider<GetTasksForOrganizationQuery> sutProvider)
    {
        var status = SecurityTaskStatus.Pending;
        sutProvider.GetDependency<ICurrentContext>().HttpContext.User.Returns(new ClaimsPrincipal());
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(org.Id).Returns(org);
        sutProvider.GetDependency<IAuthorizationService>().AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(), org, Arg.Is<IEnumerable<IAuthorizationRequirement>>(
                e => e.Contains(SecurityTaskOperations.ListAllForOrganization)
            )
        ).Returns(AuthorizationResult.Success());
        sutProvider.GetDependency<ISecurityTaskRepository>().GetManyByOrganizationIdStatusAsync(org.Id, status).Returns(new List<SecurityTask>()
        {
            new() { Id = Guid.NewGuid() },
            new() { Id = Guid.NewGuid() },
        });

        var result = await sutProvider.Sut.GetTasksAsync(org.Id, status);

        Assert.Equal(2, result.Count);
        await sutProvider.GetDependency<IAuthorizationService>().Received(1).AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(), org, Arg.Is<IEnumerable<IAuthorizationRequirement>>(
                e => e.Contains(SecurityTaskOperations.ListAllForOrganization)
            )
        );
        await sutProvider.GetDependency<ISecurityTaskRepository>().Received(1).GetManyByOrganizationIdStatusAsync(org.Id, SecurityTaskStatus.Pending);
    }

    [Theory, BitAutoData]
    public async Task GetTaskAsync_MissingOrg_Failure(Guid userId, SutProvider<GetTasksForOrganizationQuery> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(Arg.Any<Guid>()).Returns((CurrentContextOrganization)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetTasksAsync(Guid.NewGuid()));
    }

    [Theory, BitAutoData]
    public async Task GetTaskAsync_MissingUser_Failure(CurrentContextOrganization org, SutProvider<GetTasksForOrganizationQuery> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(null as Guid?);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(org.Id).Returns(org);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetTasksAsync(org.Id));
    }

    [Theory, BitAutoData]
    public async Task GetTasksAsync_Unauthorized_Failure(
        Guid userId, CurrentContextOrganization org,
        SutProvider<GetTasksForOrganizationQuery> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().HttpContext.User.Returns(new ClaimsPrincipal());
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(org.Id).Returns(org);
        sutProvider.GetDependency<IAuthorizationService>().AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(), org, Arg.Is<IEnumerable<IAuthorizationRequirement>>(
                e => e.Contains(SecurityTaskOperations.ListAllForOrganization)
            )
        ).Returns(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetTasksAsync(org.Id));

        await sutProvider.GetDependency<IAuthorizationService>().Received(1).AuthorizeAsync(
            Arg.Any<ClaimsPrincipal>(), org, Arg.Is<IEnumerable<IAuthorizationRequirement>>(
                e => e.Contains(SecurityTaskOperations.ListAllForOrganization)
            )
        );
        await sutProvider.GetDependency<ISecurityTaskRepository>().Received(0).GetManyByOrganizationIdStatusAsync(org.Id, SecurityTaskStatus.Pending);
    }
}
