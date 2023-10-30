
using Bit.Api.Vault;
using Bit.Api.Vault.Models.Request;
using Bit.Core.Exceptions;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Vault.Validators;

[SutProviderCustomize]
public class FolderRotationValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_MissingFolder_Throws(SutProvider<FolderRotationValidator> sutProvider, Guid userId, IEnumerable<FolderWithIdRequestModel> folders)
    {
        var userFolders = folders.Select(f => f.ToFolder(new Folder())).ToList();
        userFolders.Add(new Folder { Id = Guid.NewGuid(), Name = "Missing Folder" });
        sutProvider.GetDependency<IFolderRepository>().GetManyByUserIdAsync(userId).Returns(userFolders);


        await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.ValidateAsync(userId, folders));
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_FolderDoesNotBelongToUser_NotIncluded(SutProvider<FolderRotationValidator> sutProvider, Guid userId, IEnumerable<FolderWithIdRequestModel> folders)
    {
        var userFolders = folders.Select(f => f.ToFolder(new Folder())).ToList();
        userFolders.RemoveAt(0);
        sutProvider.GetDependency<IFolderRepository>().GetManyByUserIdAsync(userId).Returns(userFolders);

        var result = await sutProvider.Sut.ValidateAsync(userId, folders);

        Assert.DoesNotContain(result, c => c.Id == folders.First().Id);
    }
}
