using Bit.Commercial.Core.SecretsManager.Commands.PasswordManager;
using Bit.Commercial.Core.SecretsManager.Commands.Projects;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Core.Test.SecretsManager.AutoFixture.SecretsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.Projects;

[SutProviderCustomize]
[ProjectCustomize]
[SecretCustomize]
public class RequestSMAccessCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task SendRequestAccessToSM_Success(
        User user,
        Organization organization,
        string emailContent,
        SutProvider<RequestSMAccessCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(Arg.Any<Guid>()).Returns(organization);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns(new OrganizationUser() { Id = user.Id });

        var result = await sutProvider.Sut.SendRequestAccessToSM(organization.Id, user, emailContent);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .GetManyDetailsByOrganizationAsync(organization.Id);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1)
          .GetByIdAsync(organization.Id);

        Assert.True(result);
    }
}
