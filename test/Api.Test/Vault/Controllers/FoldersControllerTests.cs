using System.ComponentModel.DataAnnotations;
using Bit.Api.Vault.Controllers;
using Bit.Api.Vault.Models.Request;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Vault.Commands.Interfaces;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Api.Test.Vault.Controllers;

[ControllerCustomize(typeof(FoldersController))]
[SutProviderCustomize]
public class FoldersControllerTests
{
    [Theory, BitAutoData]
    public async Task DeleteMany_PassesIdsToCommandForTheCallingUser(
        SutProvider<FoldersController> sutProvider, Guid userId, Guid firstFolderId, Guid secondFolderId)
    {
        sutProvider.GetDependency<GlobalSettings>().SelfHosted = false;
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        var model = new FolderBulkDeleteRequestModel { Ids = [firstFolderId, secondFolderId] };

        await sutProvider.Sut.DeleteMany(model);

        await sutProvider.GetDependency<IDeleteManyFoldersCommand>().Received(1).DeleteManyAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { firstFolderId, secondFolderId })),
            userId);
    }

    [Fact]
    public void FolderBulkDeleteRequestModel_WithNullIds_FailsValidation()
    {
        var model = new FolderBulkDeleteRequestModel { Ids = null };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results, true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(FolderBulkDeleteRequestModel.Ids)));
    }

    [Theory, BitAutoData]
    public async Task DeleteMany_WithMoreThan500IdsOnCloud_ThrowsBadRequest(
        SutProvider<FoldersController> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<GlobalSettings>().SelfHosted = false;
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        var model = new FolderBulkDeleteRequestModel
        {
            Ids = Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()).ToList()
        };

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.DeleteMany(model));

        await sutProvider.GetDependency<IDeleteManyFoldersCommand>().DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task DeleteMany_WithMoreThan500IdsSelfHosted_IsAllowed(
        SutProvider<FoldersController> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<GlobalSettings>().SelfHosted = true;
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        var model = new FolderBulkDeleteRequestModel
        {
            Ids = Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()).ToList()
        };

        await sutProvider.Sut.DeleteMany(model);

        await sutProvider.GetDependency<IDeleteManyFoldersCommand>().Received(1)
            .DeleteManyAsync(Arg.Any<IEnumerable<Guid>>(), userId);
    }
}
