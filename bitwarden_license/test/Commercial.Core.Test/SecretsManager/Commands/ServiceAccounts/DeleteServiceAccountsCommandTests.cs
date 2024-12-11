using Bit.Commercial.Core.SecretsManager.Commands.ServiceAccounts;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.ServiceAccounts;

[SutProviderCustomize]
public class DeleteServiceAccountsCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task DeleteServiceAccounts_Success(
        SutProvider<DeleteServiceAccountsCommand> sutProvider,
        List<ServiceAccount> data
    )
    {
        await sutProvider.Sut.DeleteServiceAccounts(data);
        await sutProvider
            .GetDependency<IServiceAccountRepository>()
            .Received(1)
            .DeleteManyByIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data.Select(d => d.Id))));
    }
}
