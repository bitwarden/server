using Bit.Api.SecretsManager.Controllers;
using Bit.Api.SecretsManager.Models.Request;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.SecretsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.SecretsManager.Controllers;

[ControllerCustomize(typeof(SecretsController))]
[SutProviderCustomize]
[JsonDocumentCustomize]
[SecretCustomize]
public class SecretsControllerTests
{
    [Theory]
    [BitAutoData]
    public async void GetSecretsByOrganization_ReturnsEmptyList(SutProvider<SecretsController> sutProvider, Guid id)
    {
        var result = await sutProvider.Sut.GetSecretsByOrganizationAsync(id);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
                     .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(id)));

        Assert.Empty(result.Secrets);
    }

    [Theory]
    [BitAutoData]
    public async void GetSecret_NotFound(SutProvider<SecretsController> sutProvider)
    {
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetSecretAsync(Guid.NewGuid()));
    }

    [Theory]
    [BitAutoData]
    public async void GetSecret_Success(SutProvider<SecretsController> sutProvider, Secret resultSecret)
    {
        sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(default).ReturnsForAnyArgs(resultSecret);

        var result = await sutProvider.Sut.GetSecretAsync(resultSecret.Id);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
                     .GetByIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(resultSecret.Id)));
    }

    [Theory]
    [BitAutoData]
    public async void GetSecretsByOrganization_Success(SutProvider<SecretsController> sutProvider, Secret resultSecret)
    {
        sutProvider.GetDependency<ISecretRepository>().GetManyByOrganizationIdAsync(default).ReturnsForAnyArgs(new List<Secret>() { resultSecret });

        var result = await sutProvider.Sut.GetSecretsByOrganizationAsync(resultSecret.OrganizationId);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
                     .GetManyByOrganizationIdAsync(Arg.Is(AssertHelper.AssertPropertyEqual(resultSecret.OrganizationId)));
    }

    [Theory]
    [BitAutoData]
    public async void CreateSecret_Success(SutProvider<SecretsController> sutProvider, SecretCreateRequestModel data, Guid organizationId)
    {
        var resultSecret = data.ToSecret(organizationId);

        sutProvider.GetDependency<ICreateSecretCommand>().CreateAsync(default).ReturnsForAnyArgs(resultSecret);

        var result = await sutProvider.Sut.CreateSecretAsync(organizationId, data);
        await sutProvider.GetDependency<ICreateSecretCommand>().Received(1)
                     .CreateAsync(Arg.Any<Secret>());
    }

    [Theory]
    [BitAutoData]
    public async void UpdateSecret_Success(SutProvider<SecretsController> sutProvider, SecretUpdateRequestModel data, Guid secretId, Guid organizationId)
    {
        var resultSecret = data.ToSecret(secretId, organizationId);
        sutProvider.GetDependency<IUpdateSecretCommand>().UpdateAsync(default).ReturnsForAnyArgs(resultSecret);

        var result = await sutProvider.Sut.UpdateSecretAsync(secretId, data);
        await sutProvider.GetDependency<IUpdateSecretCommand>().Received(1)
                     .UpdateAsync(Arg.Any<Secret>());
    }

    [Theory]
    [BitAutoData]
    public async void BulkDeleteSecret_Success(SutProvider<SecretsController> sutProvider, List<Secret> data, Guid organizationId)
    {
        var ids = data.Select(secret => secret.Id).ToList();
        var mockResult = new List<Tuple<Secret, string>>();
        foreach (var secret in data)
        {
            mockResult.Add(new Tuple<Secret, string>(secret, ""));
        }
        sutProvider.GetDependency<IDeleteSecretCommand>().DeleteSecrets(ids).ReturnsForAnyArgs(mockResult);

        var results = await sutProvider.Sut.BulkDeleteAsync(ids, organizationId);
        await sutProvider.GetDependency<IDeleteSecretCommand>().Received(1)
                     .DeleteSecrets(Arg.Is(ids));
        Assert.Equal(data.Count, results.Data.Count());
    }

    [Theory]
    [BitAutoData]
    public async void BulkDeleteSecret_NoGuids_ThrowsArgumentNullException(SutProvider<SecretsController> sutProvider)
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.BulkDeleteAsync(new List<Guid>()));
    }
}
