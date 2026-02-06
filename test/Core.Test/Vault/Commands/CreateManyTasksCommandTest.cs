using System.Security.Claims;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Test.Vault.AutoFixture;
using Bit.Core.Vault.Authorization.SecurityTasks;
using Bit.Core.Vault.Commands;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Api;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.Commands;

[SutProviderCustomize]
[SecurityTaskCustomize]
public class CreateManyTasksCommandTest
{
    private static void Setup(SutProvider<CreateManyTasksCommand> sutProvider, Guid? userId,
        bool authorizedCreate = false)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(SecurityTaskOperations.Create)))
            .Returns(authorizedCreate ? AuthorizationResult.Success() : AuthorizationResult.Failed());
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_NotLoggedIn_NotFoundException(
        SutProvider<CreateManyTasksCommand> sutProvider,
        Guid organizationId,
        IEnumerable<SecurityTaskCreateRequest> tasks)
    {
        Setup(sutProvider, null, true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CreateAsync(organizationId, tasks));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_NoTasksProvided_BadRequestException(
        SutProvider<CreateManyTasksCommand> sutProvider,
        Guid organizationId)
    {
        Setup(sutProvider, Guid.NewGuid());

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(organizationId, null));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_AuthorizationFailed_NotFoundException(
        SutProvider<CreateManyTasksCommand> sutProvider,
        Guid organizationId,
        IEnumerable<SecurityTaskCreateRequest> tasks)
    {
        Setup(sutProvider, Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CreateAsync(organizationId, tasks));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_AuthorizationSucceeded_ReturnsSecurityTasks(
        SutProvider<CreateManyTasksCommand> sutProvider,
        Guid organizationId,
        IEnumerable<SecurityTaskCreateRequest> tasks,
        ICollection<SecurityTask> securityTasks)
    {
        Setup(sutProvider, Guid.NewGuid(), true);
        sutProvider.GetDependency<ISecurityTaskRepository>()
            .CreateManyAsync(Arg.Any<ICollection<SecurityTask>>())
            .Returns(securityTasks);

        var result = await sutProvider.Sut.CreateAsync(organizationId, tasks);

        Assert.Equal(securityTasks, result);
    }
}
