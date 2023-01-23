using Bit.Commercial.Core.SecretManager.Projects;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Test.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretManagerFeatures.Projects;

[SutProviderCustomize]
[ProjectCustomize]
public class UpdateProjectCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_Throws_NotFoundException(Project project, Guid userId, SutProvider<UpdateProjectCommand> sutProvider)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateAsync(project, userId));

        await sutProvider.GetDependency<IProjectRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_Admin_Succeeds(Project project, Guid userId, SutProvider<UpdateProjectCommand> sutProvider)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);
        sutProvider.GetDependency<ICurrentContext>().OrganizationAdmin(project.OrganizationId).Returns(true);

        var project2 = new Project { Id = project.Id, Name = "newName" };
        var result = await sutProvider.Sut.UpdateAsync(project2, userId);

        Assert.NotNull(result);
        Assert.Equal("newName", result.Name);
        AssertHelper.AssertRecent(result.RevisionDate);

        await sutProvider.GetDependency<IProjectRepository>().ReceivedWithAnyArgs(1).ReplaceAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_User_NoAccess(Project project, Guid userId, SutProvider<UpdateProjectCommand> sutProvider)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);
        sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(project.Id, userId).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sutProvider.Sut.UpdateAsync(project, userId));

        await sutProvider.GetDependency<IProjectRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_User_Success(Project project, Guid userId, SutProvider<UpdateProjectCommand> sutProvider)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);
        sutProvider.GetDependency<IProjectRepository>().UserHasWriteAccessToProject(project.Id, userId).Returns(true);

        var project2 = new Project { Id = project.Id, Name = "newName" };
        var result = await sutProvider.Sut.UpdateAsync(project2, userId);

        Assert.NotNull(result);
        Assert.Equal("newName", result.Name);

        await sutProvider.GetDependency<IProjectRepository>().ReceivedWithAnyArgs(1).ReplaceAsync(default);
    }
}
