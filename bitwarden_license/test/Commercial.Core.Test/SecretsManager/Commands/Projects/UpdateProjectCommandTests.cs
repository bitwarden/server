using Bit.Commercial.Core.SecretsManager.Commands.Projects;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.Projects;

[SutProviderCustomize]
[ProjectCustomize]
public class UpdateProjectCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_Throws_NotFoundException(Project project, SutProvider<UpdateProjectCommand> sutProvider)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).ReturnsNull();

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateAsync(project));

        await sutProvider.GetDependency<IProjectRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_Success(Project project, SutProvider<UpdateProjectCommand> sutProvider)
    {
        sutProvider.GetDependency<IProjectRepository>().GetByIdAsync(project.Id).Returns(project);

        var updatedProject = new Project { Id = project.Id, Name = "newName" };
        var result = await sutProvider.Sut.UpdateAsync(updatedProject);

        Assert.NotNull(result);
        Assert.Equal("newName", result.Name);

        await sutProvider.GetDependency<IProjectRepository>().ReceivedWithAnyArgs(1).ReplaceAsync(default);
    }
}
