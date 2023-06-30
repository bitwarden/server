using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Commands.EnableAccessSecretsManager;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.SecretsManager.Commands.EnableAccessSecretsManager;

[SutProviderCustomize]
public class EnableAccessSecretsManagerCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task EnableUsers_UsersAlreadyEnabled_DoesNotCallRepository(
        SutProvider<EnableAccessSecretsManagerCommand> sutProvider, ICollection<OrganizationUser> data)
    {
        foreach (var item in data)
        {
            item.AccessSecretsManager = true;
        }

        var result = await sutProvider.Sut.EnableUsersAsync(data);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceManyAsync(default);

        Assert.Equal(data.Count, result.Count);
        Assert.Equal(data.Count,
            result.Where(x => x.error == "User already has access to Secrets Manager").ToList().Count);
    }

    [Theory]
    [BitAutoData]
    public async Task EnableUsers_OneUserNotEnabled_CallsRepositoryForOne(
        SutProvider<EnableAccessSecretsManagerCommand> sutProvider, ICollection<OrganizationUser> data)
    {
        var firstUser = new List<OrganizationUser>();
        foreach (var item in data)
        {
            if (item == data.First())
            {
                item.AccessSecretsManager = false;
                firstUser.Add(item);
            }
            else
            {
                item.AccessSecretsManager = true;
            }
        }

        var result = await sutProvider.Sut.EnableUsersAsync(data);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .ReplaceManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual(firstUser)));

        Assert.Equal(data.Count, result.Count);
        Assert.Equal(data.Count - 1,
            result.Where(x => x.error == "User already has access to Secrets Manager").ToList().Count);
    }

    [Theory]
    [BitAutoData]
    public async Task EnableUsers_Success(
        SutProvider<EnableAccessSecretsManagerCommand> sutProvider, ICollection<OrganizationUser> data)
    {
        foreach (var item in data)
        {
            item.AccessSecretsManager = false;
        }

        var result = await sutProvider.Sut.EnableUsersAsync(data);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .ReplaceManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)));

        Assert.Equal(data.Count, result.Count);
    }
}
