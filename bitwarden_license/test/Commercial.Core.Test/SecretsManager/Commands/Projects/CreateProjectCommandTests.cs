using Bit.Commercial.Core.SecretsManager.Commands.Projects;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.Projects;

[SutProviderCustomize]
[ProjectCustomize]
public class CreateProjectCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task CreateAsync_CallsCreate(Project data,
        Guid userId,
        SutProvider<CreateProjectCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns(new OrganizationUser() { Id = userId });

        sutProvider.GetDependency<IProjectRepository>()
            .CreateAsync(Arg.Any<Project>())
            .Returns(data);

        await sutProvider.Sut.CreateAsync(data, userId);

        await sutProvider.GetDependency<IProjectRepository>().Received(1)
            .CreateAsync(Arg.Is(data));

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .CreateManyAsync(Arg.Any<List<BaseAccessPolicy>>());
    }
}
