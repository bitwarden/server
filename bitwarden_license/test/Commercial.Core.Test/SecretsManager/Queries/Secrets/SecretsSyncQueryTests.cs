#nullable enable
using Bit.Commercial.Core.SecretsManager.Queries.Secrets;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Queries.Secrets;

[SutProviderCustomize]
public class SecretsSyncQueryTests
{
    [Theory, BitAutoData]
    public async Task GetAsync_NullLastSyncedDate_ReturnsHasChanges(
        SutProvider<SecretsSyncQuery> sutProvider,
        SecretsSyncRequest data)
    {
        data.LastSyncedDate = null;

        var result = await sutProvider.Sut.GetAsync(data);

        Assert.True(result.HasChanges);
        await sutProvider.GetDependency<ISecretRepository>().Received(1)
            .GetManyByOrganizationIdAsync(Arg.Is(data.OrganizationId),
                Arg.Is(data.ServiceAccountId),
                Arg.Is(data.AccessClientType));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_HasLastSyncedDateServiceAccountNotFound_Throws(
        SutProvider<SecretsSyncQuery> sutProvider,
        SecretsSyncRequest data)
    {
        data.LastSyncedDate = DateTime.UtcNow;
        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.ServiceAccountId)
            .Returns((ServiceAccount?)null);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.GetAsync(data));

        await sutProvider.GetDependency<ISecretRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>());
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task GetAsync_HasLastSyncedDateServiceAccountWithLaterOrEqualRevisionDate_ReturnsChanges(
        bool datesEqual,
        SutProvider<SecretsSyncQuery> sutProvider,
        SecretsSyncRequest data,
        ServiceAccount serviceAccount)
    {
        data.LastSyncedDate = DateTime.UtcNow.AddDays(-1);
        serviceAccount.Id = data.ServiceAccountId;
        serviceAccount.RevisionDate = datesEqual ? data.LastSyncedDate.Value : data.LastSyncedDate.Value.AddSeconds(600);

        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.ServiceAccountId)
            .Returns(serviceAccount);

        var result = await sutProvider.Sut.GetAsync(data);

        Assert.True(result.HasChanges);
        await sutProvider.GetDependency<ISecretRepository>().Received(1)
            .GetManyByOrganizationIdAsync(Arg.Is(data.OrganizationId),
                Arg.Is(data.ServiceAccountId),
                Arg.Is(data.AccessClientType));
    }

    [Theory, BitAutoData]
    public async Task GetAsync_HasLastSyncedDateServiceAccountWithEarlierRevisionDate_ReturnsNoChanges(
        SutProvider<SecretsSyncQuery> sutProvider,
        SecretsSyncRequest data,
        ServiceAccount serviceAccount)
    {
        data.LastSyncedDate = DateTime.UtcNow.AddDays(-1);
        serviceAccount.Id = data.ServiceAccountId;
        serviceAccount.RevisionDate = data.LastSyncedDate.Value.AddDays(-2);

        sutProvider.GetDependency<IServiceAccountRepository>().GetByIdAsync(data.ServiceAccountId)
            .Returns(serviceAccount);

        var result = await sutProvider.Sut.GetAsync(data);

        Assert.False(result.HasChanges);
        Assert.Null(result.Secrets);
        await sutProvider.GetDependency<ISecretRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<AccessClientType>());
    }
}
