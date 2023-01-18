using Bit.Commercial.Core.SecretManagerFeatures.Secrets;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Test.AutoFixture.SecretsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretManagerFeatures.Secrets;

[SutProviderCustomize]
[SecretCustomize]
public class UpdateSecretCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_SecretDoesNotExist_ThrowsNotFound(Secret data, SutProvider<UpdateSecretCommand> sutProvider)
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateAsync(data));

        await sutProvider.GetDependency<ISecretRepository>().DidNotReceiveWithAnyArgs().UpdateAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_CallsReplaceAsync(Secret data, SutProvider<UpdateSecretCommand> sutProvider)
    {
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(data.Id).Returns(data);
        await sutProvider.Sut.UpdateAsync(data);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
            .UpdateAsync(data);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_DoesNotModifyOrganizationId(Secret existingSecret, SutProvider<UpdateSecretCommand> sutProvider)
    {
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(existingSecret.Id).Returns(existingSecret);

        var updatedOrgId = Guid.NewGuid();
        var secretUpdate = new Secret()
        {
            OrganizationId = updatedOrgId,
            Id = existingSecret.Id,
            Key = existingSecret.Key,
        };

        var result = await sutProvider.Sut.UpdateAsync(secretUpdate);

        Assert.Equal(existingSecret.OrganizationId, result.OrganizationId);
        Assert.NotEqual(existingSecret.OrganizationId, updatedOrgId);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_DoesNotModifyCreationDate(Secret existingSecret, SutProvider<UpdateSecretCommand> sutProvider)
    {
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(existingSecret.Id).Returns(existingSecret);

        var updatedCreationDate = DateTime.UtcNow;
        var secretUpdate = new Secret()
        {
            CreationDate = updatedCreationDate,
            Id = existingSecret.Id,
            Key = existingSecret.Key,
        };

        var result = await sutProvider.Sut.UpdateAsync(secretUpdate);

        Assert.Equal(existingSecret.CreationDate, result.CreationDate);
        Assert.NotEqual(existingSecret.CreationDate, updatedCreationDate);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_DoesNotModifyDeletionDate(Secret existingSecret, SutProvider<UpdateSecretCommand> sutProvider)
    {
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(existingSecret.Id).Returns(existingSecret);

        var updatedDeletionDate = DateTime.UtcNow;
        var secretUpdate = new Secret()
        {
            DeletedDate = updatedDeletionDate,
            Id = existingSecret.Id,
            Key = existingSecret.Key,
        };

        var result = await sutProvider.Sut.UpdateAsync(secretUpdate);

        Assert.Equal(existingSecret.DeletedDate, result.DeletedDate);
        Assert.NotEqual(existingSecret.DeletedDate, updatedDeletionDate);
    }


    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_RevisionDateIsUpdatedToUtcNow(Secret existingSecret, SutProvider<UpdateSecretCommand> sutProvider)
    {
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(existingSecret.Id).Returns(existingSecret);

        var updatedRevisionDate = DateTime.UtcNow.AddDays(10);
        var secretUpdate = new Secret()
        {
            RevisionDate = updatedRevisionDate,
            Id = existingSecret.Id,
            Key = existingSecret.Key,
        };

        var result = await sutProvider.Sut.UpdateAsync(secretUpdate);

        Assert.NotEqual(existingSecret.RevisionDate, result.RevisionDate);
        AssertHelper.AssertRecent(result.RevisionDate);
    }
}

