using Bit.Commercial.Core.SecretManagerFeatures.Projects;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretManagerFeatures.Projects;

[SutProviderCustomize]
public class DeleteProjectCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task DeleteProjects_Throws_NotFoundException(List<Guid> data,
      SutProvider<DeleteProjectCommand> sutProvider)
    {
        sutProvider.GetDependency<IProjectRepository>().GetManyByIds(data).Returns(new List<Project>());

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteProjects(data));

        await sutProvider.GetDependency<IProjectRepository>().DidNotReceiveWithAnyArgs().DeleteManyByIdAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteSecrets_OneIdNotFound_Throws_NotFoundException(List<Guid> data,
      SutProvider<DeleteProjectCommand> sutProvider)
    {
        var project = new Project()
        {
            Id = Guid.NewGuid()
        };
        sutProvider.GetDependency<IProjectRepository>().GetManyByIds(data).Returns(new List<Project>() { project });

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteProjects(data));

        await sutProvider.GetDependency<IProjectRepository>().DidNotReceiveWithAnyArgs().DeleteManyByIdAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteSecrets_Success(List<Guid> data,
      SutProvider<DeleteProjectCommand> sutProvider)
    {
        var projects = new List<Project>();
        foreach (Guid id in data)
        {
            var project = new Project()
            {
                Id = id
            };
            projects.Add(project);
        }

        sutProvider.GetDependency<IProjectRepository>().GetManyByIds(data).Returns(projects);

        var results = await sutProvider.Sut.DeleteProjects(data);

        await sutProvider.GetDependency<IProjectRepository>().Received(1).DeleteManyByIdAsync(Arg.Is(data));
        foreach (var result in results)
        {
            Assert.Equal("", result.Item2);
        }
    }
}

