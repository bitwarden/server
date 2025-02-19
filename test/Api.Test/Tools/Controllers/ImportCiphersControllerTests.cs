using AutoFixture;
using Bit.Api.Tools.Controllers;
using Bit.Api.Tools.Models.Request.Accounts;
using Bit.Api.Vault.Models.Request;
using Bit.Core.Exceptions;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Tools.Controllers;

[ControllerCustomize(typeof(ImportCiphersController))]
[SutProviderCustomize]
public class ImportCiphersControllerTests
{
    [Theory, BitAutoData]
    public async Task PostImport_ImportCiphersRequestModel_BadRequestException(SutProvider<ImportCiphersController> sutProvider, IFixture fixture)
    {
        // Arrange
        sutProvider.GetDependency<GlobalSettings>()
            .SelfHosted = false;
        var ciphers = fixture.CreateMany<CipherRequestModel>(7001).ToArray();
        var model = new ImportCiphersRequestModel
        {
            Ciphers = ciphers,
            FolderRelationships = null,
            Folders = null
        };

        // Act
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PostImport(model));

        // Assert
        Assert.Equal("You cannot import this much data at once.", exception.Message);
    }

    // [Theory, BitAutoData]
    // public async Task PostImport_ImportCiphersRequestModel_Success
    // {
    //     // Arrange
    //
    //     // Act
    //
    //     // Assert
    // }
}
