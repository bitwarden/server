using Bit.Commercial.Core.SecretsManager.Commands.Projects;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Identity;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Projects;

[SutProviderCustomize]
public class DeleteProjectCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task DeleteProjects_Throws_NotFoundException(List<Guid> data, Guid userId,
      SutProvider<DeleteProjectCommand> sutProvider)
    {
        sutProvider.GetDependency<IProjectRepository>().GetManyByIds(data).Returns(new List<Project>());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteProjects(data, userId));

        await sutProvider.GetDependency<IProjectRepository>().DidNotReceiveWithAnyArgs().DeleteManyByIdAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteSecrets_OneIdNotFound_Throws_NotFoundException(List<Guid> data, Guid userId,
      SutProvider<DeleteProjectCommand> sutProvider)
    {
        var project = new Project()
        {
            Id = Guid.NewGuid()
        };
        sutProvider.GetDependency<IProjectRepository>().GetManyByIds(data).Returns(new List<Project>() { project });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteProjects(data, userId));

        await sutProvider.GetDependency<IProjectRepository>().DidNotReceiveWithAnyArgs().DeleteManyByIdAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteSecrets_User_Success(List<Guid> data, Guid userId, Guid organizationId,
        SutProvider<DeleteProjectCommand> sutProvider)
    {
        var projects = data.Select(id => new Project { Id = id, OrganizationId = organizationId }).ToList();

        sutProvider.GetDependency<ICurrentContext>().ClientType = ClientType.User;
        sutProvider.GetDependency<IProjectRepository>().GetManyByIds(data).Returns(projects);
        sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(Arg.Any<Guid>(), userId).Returns(true);

        var results = await sutProvider.Sut.DeleteProjects(data, userId);

        foreach (var result in results)
        {
            Assert.Equal("", result.Item2);
        }

        await sutProvider.GetDependency<IProjectRepository>().Received(1).DeleteManyByIdAsync(Arg.Is<List<Guid>>(d => d.SequenceEqual(data)));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteSecrets_User_No_Permission(List<Guid> data, Guid userId, Guid organizationId,
        SutProvider<DeleteProjectCommand> sutProvider)
    {
        var projects = data.Select(id => new Project { Id = id, OrganizationId = organizationId }).ToList();

        sutProvider.GetDependency<ICurrentContext>().ClientType = ClientType.User;
        sutProvider.GetDependency<IProjectRepository>().GetManyByIds(data).Returns(projects);
        sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(userId, userId).Returns(false);

        var results = await sutProvider.Sut.DeleteProjects(data, userId);

        foreach (var result in results)
        {
            Assert.Equal("access denied", result.Item2);
        }

        await sutProvider.GetDependency<IProjectRepository>().DidNotReceiveWithAnyArgs().DeleteManyByIdAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteSecrets_OrganizationAdmin_Success(List<Guid> data, Guid userId, Guid organizationId,
      SutProvider<DeleteProjectCommand> sutProvider)
    {
        var projects = data.Select(id => new Project { Id = id, OrganizationId = organizationId }).ToList();

        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(organizationId).Returns(true);
        sutProvider.GetDependency<IProjectRepository>().GetManyByIds(data).Returns(projects);

        var results = await sutProvider.Sut.DeleteProjects(data, userId);

        await sutProvider.GetDependency<IProjectRepository>().Received(1).DeleteManyByIdAsync(Arg.Is<List<Guid>>(d => d.SequenceEqual(data)));
        foreach (var result in results)
        {
            Assert.Equal("", result.Item2);
        }
    }
}
