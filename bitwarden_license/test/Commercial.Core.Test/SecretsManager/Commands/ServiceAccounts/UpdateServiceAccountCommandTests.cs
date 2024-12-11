using Bit.Commercial.Core.SecretsManager.Commands.ServiceAccounts;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.ServiceAccounts;

[SutProviderCustomize]
public class UpdateServiceAccountCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_ServiceAccountDoesNotExist_ThrowsNotFound(
        ServiceAccount data,
        SutProvider<UpdateServiceAccountCommand> sutProvider
    )
    {
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateAsync(data));

        await sutProvider
            .GetDependency<IServiceAccountRepository>()
            .DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_Success(
        ServiceAccount data,
        SutProvider<UpdateServiceAccountCommand> sutProvider
    )
    {
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.Id).Returns(data);

        await sutProvider.Sut.UpdateAsync(data);

        await sutProvider
            .GetDependency<IServiceAccountRepository>()
            .Received(1)
            .ReplaceAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_DoesNotModifyOrganizationId(
        ServiceAccount existingServiceAccount,
        SutProvider<UpdateServiceAccountCommand> sutProvider
    )
    {
        sutProvider
            .GetDependency<IServiceAccountRepository>()
            .GetByIdAsync(existingServiceAccount.Id)
            .Returns(existingServiceAccount);

        var updatedOrgId = Guid.NewGuid();
        var serviceAccountUpdate = new ServiceAccount()
        {
            OrganizationId = updatedOrgId,
            Id = existingServiceAccount.Id,
            Name = existingServiceAccount.Name,
        };

        var result = await sutProvider.Sut.UpdateAsync(serviceAccountUpdate);

        Assert.Equal(existingServiceAccount.OrganizationId, result.OrganizationId);
        Assert.NotEqual(existingServiceAccount.OrganizationId, updatedOrgId);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_DoesNotModifyCreationDate(
        ServiceAccount existingServiceAccount,
        SutProvider<UpdateServiceAccountCommand> sutProvider
    )
    {
        sutProvider
            .GetDependency<IServiceAccountRepository>()
            .GetByIdAsync(existingServiceAccount.Id)
            .Returns(existingServiceAccount);

        var updatedCreationDate = DateTime.UtcNow;
        var serviceAccountUpdate = new ServiceAccount()
        {
            CreationDate = updatedCreationDate,
            Id = existingServiceAccount.Id,
            Name = existingServiceAccount.Name,
        };

        var result = await sutProvider.Sut.UpdateAsync(serviceAccountUpdate);

        Assert.Equal(existingServiceAccount.CreationDate, result.CreationDate);
        Assert.NotEqual(existingServiceAccount.CreationDate, updatedCreationDate);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_RevisionDateIsUpdatedToUtcNow(
        ServiceAccount existingServiceAccount,
        SutProvider<UpdateServiceAccountCommand> sutProvider
    )
    {
        sutProvider
            .GetDependency<IServiceAccountRepository>()
            .GetByIdAsync(existingServiceAccount.Id)
            .Returns(existingServiceAccount);

        var updatedRevisionDate = DateTime.UtcNow.AddDays(10);
        var serviceAccountUpdate = new ServiceAccount()
        {
            RevisionDate = updatedRevisionDate,
            Id = existingServiceAccount.Id,
            Name = existingServiceAccount.Name,
        };

        var result = await sutProvider.Sut.UpdateAsync(serviceAccountUpdate);

        Assert.NotEqual(serviceAccountUpdate.RevisionDate, result.RevisionDate);
        AssertHelper.AssertRecent(result.RevisionDate);
    }
}
