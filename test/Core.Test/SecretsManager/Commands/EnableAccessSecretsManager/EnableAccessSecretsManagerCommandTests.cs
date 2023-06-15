using Bit.Core.Entities;
using Bit.Core.Exceptions;
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
    public async Task EnableUsers_UsersAndOrgMismatch_ThrowsBadRequestException(
        SutProvider<EnableAccessSecretsManagerCommand> sutProvider, ICollection<OrganizationUser> data, Guid orgId)
    {
        foreach (var item in data)
        {
            item.OrganizationId = new Guid();
        }

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.EnableUsersAsync(orgId, data));

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceManyAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task EnableUsers_UsersAlreadyEnabled_DoesNotCallRepository(
        SutProvider<EnableAccessSecretsManagerCommand> sutProvider, ICollection<OrganizationUser> data, Guid orgId)
    {
        foreach (var item in data)
        {
            item.OrganizationId = orgId;
            item.AccessSecretsManager = true;
        }

        var result = await sutProvider.Sut.EnableUsersAsync(orgId, data);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceManyAsync(default);

        Assert.Equal(data.Count, result.Count);
    }

    [Theory]
    [BitAutoData]
    public async Task EnableUsers_OneUserNotEnabled_CallsRepositoryForOne(
        SutProvider<EnableAccessSecretsManagerCommand> sutProvider, ICollection<OrganizationUser> data, Guid orgId)
    {
        var firstUser = new List<OrganizationUser>();
        foreach (var item in data)
        {
            if (item == data.First())
            {
                item.OrganizationId = orgId;
                item.AccessSecretsManager = false;
                firstUser.Add(item);
            }
            else
            {
                item.OrganizationId = orgId;
                item.AccessSecretsManager = true;
            }
        }

        var result = await sutProvider.Sut.EnableUsersAsync(orgId, data);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .ReplaceManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual(firstUser)));

        Assert.Equal(data.Count, result.Count);
    }

    [Theory]
    [BitAutoData]
    public async Task EnableUsers_OneUserNotInOrg_DoesNotUpdateUser(
        SutProvider<EnableAccessSecretsManagerCommand> sutProvider, ICollection<OrganizationUser> data, Guid orgId)
    {
        var targetUsers = new List<OrganizationUser>();
        foreach (var item in data)
        {
            if (item == data.First())
            {
                item.OrganizationId = new Guid();
                item.AccessSecretsManager = false;
            }
            else
            {
                item.OrganizationId = orgId;
                item.AccessSecretsManager = false;
                targetUsers.Add(item);
            }
        }

        var result = await sutProvider.Sut.EnableUsersAsync(orgId, data);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .ReplaceManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual(targetUsers)));

        Assert.Equal(data.Count - 1, result.Count);
    }

    [Theory]
    [BitAutoData]
    public async Task EnableUsers_Success(
        SutProvider<EnableAccessSecretsManagerCommand> sutProvider, ICollection<OrganizationUser> data, Guid orgId)
    {
        foreach (var item in data)
        {
            item.OrganizationId = orgId;
            item.AccessSecretsManager = false;
        }

        var result = await sutProvider.Sut.EnableUsersAsync(orgId, data);

        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1)
            .ReplaceManyAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)));

        Assert.Equal(data.Count, result.Count);
    }
}
