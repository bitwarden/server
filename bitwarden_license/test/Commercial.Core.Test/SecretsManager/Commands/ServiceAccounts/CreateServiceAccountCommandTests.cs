using Bit.Commercial.Core.SecretsManager.Commands.ServiceAccounts;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.ServiceAccounts;

[SutProviderCustomize]
public class CreateServiceAccountCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task CreateAsync_CallsCreate(
        ServiceAccount data,
        Guid userId,
        SutProvider<CreateServiceAccountCommand> sutProvider
    )
    {
        sutProvider
            .GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns(new OrganizationUser() { Id = userId });

        sutProvider
            .GetDependency<IServiceAccountRepository>()
            .CreateAsync(Arg.Any<ServiceAccount>())
            .Returns(data);

        await sutProvider.Sut.CreateAsync(data, userId);

        await sutProvider
            .GetDependency<IServiceAccountRepository>()
            .Received(1)
            .CreateAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
    }
}
