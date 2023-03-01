using System.Security.Claims;
using Bit.Api.Vault.Controllers;
using Bit.Api.Vault.Models.Request;
using Bit.Core.Services;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Core.Vault.Models.Data;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Controllers;

[ControllerCustomize(typeof(CiphersController))]
[SutProviderCustomize]
public class CiphersControllerTests
{
    [Theory, BitAutoData]
    public async Task PutPartialShouldReturnCipherWithGivenFolderAndFavoriteValues(Guid userId, Guid folderId, SutProvider<CiphersController> sutProvider)
    {
        var isFavorite = true;
        var cipherId = Guid.NewGuid();

        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);

        var cipherDetails = new CipherDetails
        {
            Favorite = isFavorite,
            FolderId = folderId,
            Type = Core.Vault.Enums.CipherType.SecureNote,
            Data = "{}"
        };

        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns(Task.FromResult(cipherDetails));

        var result = await sutProvider.Sut.PutPartial(cipherId.ToString(), new CipherPartialRequestModel { Favorite = isFavorite, FolderId = folderId.ToString() });

        Assert.Equal(folderId.ToString(), result.FolderId);
        Assert.Equal(isFavorite, result.Favorite);
    }
}
