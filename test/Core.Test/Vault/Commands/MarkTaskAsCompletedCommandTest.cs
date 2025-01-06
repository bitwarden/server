#nullable enable
using System.Security.Claims;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Test.Vault.AutoFixture;
using Bit.Core.Vault.Authorization;
using Bit.Core.Vault.Commands;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.Commands;

[SutProviderCustomize]
[SecurityTaskCustomize]
public class MarkTaskAsCompletedCommandTest
{
    private static void Setup(SutProvider<MarkTaskAsCompletedCommand> sutProvider, Guid taskId, SecurityTask? securityTask, Guid? userId, bool authorizedUpdate = false)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ISecurityTaskRepository>()
            .GetByIdAsync(taskId)
            .Returns(securityTask);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), securityTask ?? Arg.Any<SecurityTask>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs =>
                    reqs.Contains(SecurityTaskOperations.Update)))
            .Returns(authorizedUpdate ? AuthorizationResult.Success() : AuthorizationResult.Failed());
    }

    [Theory]
    [BitAutoData]
    public async Task CompleteAsync_NotLoggedIn_NotFoundException(
        SutProvider<MarkTaskAsCompletedCommand> sutProvider,
        Guid taskId,
        SecurityTask securityTask)
    {
        Setup(sutProvider, taskId, securityTask, null, true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CompleteAsync(taskId));
    }

    [Theory]
    [BitAutoData]
    public async Task CompleteAsync_TaskNotFound_NotFoundException(
        SutProvider<MarkTaskAsCompletedCommand> sutProvider,
        Guid taskId)
    {
        Setup(sutProvider, taskId, null, Guid.NewGuid(), true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CompleteAsync(taskId));
    }

    [Theory]
    [BitAutoData]
    public async Task CompleteAsync_AuthorizationFailed_NotFoundException(
        SutProvider<MarkTaskAsCompletedCommand> sutProvider,
        Guid taskId,
        SecurityTask securityTask)
    {
        Setup(sutProvider, taskId, securityTask, Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CompleteAsync(taskId));
    }

    [Theory]
    [BitAutoData]
    public async Task CompleteAsync_Success(
        SutProvider<MarkTaskAsCompletedCommand> sutProvider,
        Guid taskId,
        SecurityTask securityTask)
    {
        Setup(sutProvider, taskId, securityTask, Guid.NewGuid(), true);

        await sutProvider.Sut.CompleteAsync(taskId);

        await sutProvider.GetDependency<ISecurityTaskRepository>().Received(1).ReplaceAsync(securityTask);
    }
}
