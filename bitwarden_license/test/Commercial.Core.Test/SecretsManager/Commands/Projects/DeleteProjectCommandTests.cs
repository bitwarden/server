using Bit.Commercial.Core.SecretsManager.Commands.Projects;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.Projects;

[SutProviderCustomize]
[ProjectCustomize]
public class DeleteProjectCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task DeleteProjects_Success(List<Project> data,
      SutProvider<DeleteProjectCommand> sutProvider)
    {
        await sutProvider.Sut.DeleteProjects(data);
        await sutProvider.GetDependency<IProjectRepository>()
            .Received(1)
            .DeleteManyByIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data.Select(d => d.Id))));
    }
}
