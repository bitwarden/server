using System.Text;
using System.Text.Json;
using Bit.Api.Tools.Controllers;
using Bit.Api.Tools.Models.Request;
using Bit.Api.Tools.Models.Response;
using Bit.Core.Exceptions;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.ReceiveFeatures.Commands.Interfaces;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Tools.Controllers;

[ControllerCustomize(typeof(ReceivesController))]
[SutProviderCustomize]
public class ReceivesControllerTests
{
    private static readonly ReceiveFileUploadRequestModel _uploadRequest = new()
    {
        FileName = "encrypted-filename",
        EncapsulatedFileContentEncryptionKey = "encrypted-key"
    };

    private static void SetupHttpContext(
        SutProvider<ReceivesController> sutProvider,
        string? secretHeaderValue = null)
    {
        var context = new DefaultHttpContext();
        if (secretHeaderValue is not null)
        {
            context.Request.Headers["Receive-Secret"] = secretHeaderValue;
        }
        sutProvider.Sut.ControllerContext = new ControllerContext { HttpContext = context };
    }

    [Theory, BitAutoData]
    public async Task GetShared_MissingHeader_ThrowsBadRequest(
        Guid receiveId,
        SutProvider<ReceivesController> sutProvider)
    {
        SetupHttpContext(sutProvider);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.GetShared(receiveId));
    }

    [Theory, BitAutoData]
    public async Task GetShared_ReceiveNotFound_ThrowsNotFound(
        Guid receiveId,
        SutProvider<ReceivesController> sutProvider)
    {
        SetupHttpContext(sutProvider, "some-secret");
        sutProvider.GetDependency<IReceiveRepository>()
            .GetByIdAsync(receiveId)
            .Returns((Receive?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.GetShared(receiveId));
    }

    [Theory, BitAutoData]
    public async Task GetShared_SecretMismatch_ThrowsBadRequest(
        Guid receiveId,
        Receive receive,
        SutProvider<ReceivesController> sutProvider)
    {
        receive.Secret = "correct-secret";
        SetupHttpContext(sutProvider, CoreHelpers.Base64UrlEncode(Encoding.UTF8.GetBytes("wrong-secret")));
        sutProvider.GetDependency<IReceiveRepository>()
            .GetByIdAsync(receiveId)
            .Returns(receive);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.GetShared(receiveId));
    }

    [Theory, BitAutoData]
    public async Task GetShared_ReceiveExpired_ThrowsNotFound(
        Guid receiveId,
        Receive receive,
        SutProvider<ReceivesController> sutProvider)
    {
        receive.Secret = "correct-secret";
        SetupHttpContext(sutProvider, CoreHelpers.Base64UrlEncode(Encoding.UTF8.GetBytes(receive.Secret)));
        sutProvider.GetDependency<IReceiveRepository>()
            .GetByIdAsync(receiveId)
            .Returns(receive);
        sutProvider.GetDependency<IReceiveAuthorizationService>()
            .ReceiveCanBeAccessed(receive)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.GetShared(receiveId));
    }

    [Theory, BitAutoData]
    public async Task GetShared_ValidRequest_ReturnsSharedModel(
        Guid receiveId,
        Receive receive,
        SutProvider<ReceivesController> sutProvider)
    {
        receive.Secret = "correct-secret";
        receive.Data = JsonSerializer.Serialize(new ReceiveData());
        SetupHttpContext(sutProvider, CoreHelpers.Base64UrlEncode(Encoding.UTF8.GetBytes(receive.Secret)));
        sutProvider.GetDependency<IReceiveRepository>()
            .GetByIdAsync(receiveId)
            .Returns(receive);
        sutProvider.GetDependency<IReceiveAuthorizationService>()
            .ReceiveCanBeAccessed(receive)
            .Returns(true);

        var result = await sutProvider.Sut.GetShared(receiveId);

        Assert.IsType<SharedReceiveResponseModel>(result);
        Assert.Equal(receive.Name, result.Name);
        Assert.Equal(receive.ScekWrappedPublicKey, result.ScekWrappedPublicKey);
    }

    [Theory, BitAutoData]
    public async Task GetReceiveFileUploadUrl_MissingHeader_ThrowsBadRequest(
        Guid id,
        SutProvider<ReceivesController> sutProvider)
    {
        SetupHttpContext(sutProvider);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.GetReceiveFileUploadUrl(id, _uploadRequest));
    }

    [Theory, BitAutoData]
    public async Task GetReceiveFileUploadUrl_ReceiveNotFound_ThrowsNotFound(
        Guid id,
        SutProvider<ReceivesController> sutProvider)
    {
        SetupHttpContext(sutProvider, "some-secret");
        sutProvider.GetDependency<IReceiveRepository>()
            .GetByIdAsync(id)
            .Returns((Receive?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.GetReceiveFileUploadUrl(id, _uploadRequest));
    }

    [Theory, BitAutoData]
    public async Task GetReceiveFileUploadUrl_SecretMismatch_ThrowsBadRequest(
        Guid id,
        Receive receive,
        SutProvider<ReceivesController> sutProvider)
    {
        receive.Secret = "correct-secret";
        SetupHttpContext(sutProvider, CoreHelpers.Base64UrlEncode(Encoding.UTF8.GetBytes("wrong-secret")));
        sutProvider.GetDependency<IReceiveRepository>()
            .GetByIdAsync(id)
            .Returns(receive);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.GetReceiveFileUploadUrl(id, _uploadRequest));
    }

    [Theory, BitAutoData]
    public async Task GetReceiveFileUploadUrl_ReceiveExpired_ThrowsNotFound(
        Guid id,
        Receive receive,
        SutProvider<ReceivesController> sutProvider)
    {
        receive.Secret = "correct-secret";
        SetupHttpContext(sutProvider, CoreHelpers.Base64UrlEncode(Encoding.UTF8.GetBytes(receive.Secret)));
        sutProvider.GetDependency<IReceiveRepository>()
            .GetByIdAsync(id)
            .Returns(receive);
        sutProvider.GetDependency<IReceiveAuthorizationService>()
            .ReceiveCanBeAccessed(receive)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.GetReceiveFileUploadUrl(id, _uploadRequest));
    }

    [Theory, BitAutoData]
    public async Task GetReceiveFileUploadUrl_ValidRequest_ReturnsUploadUrl(
        Guid id,
        Receive receive,
        string expectedUrl,
        SutProvider<ReceivesController> sutProvider)
    {
        receive.Secret = "correct-secret";
        SetupHttpContext(sutProvider, CoreHelpers.Base64UrlEncode(Encoding.UTF8.GetBytes(receive.Secret)));
        sutProvider.GetDependency<IReceiveRepository>()
            .GetByIdAsync(id)
            .Returns(receive);
        sutProvider.GetDependency<IReceiveAuthorizationService>()
            .ReceiveCanBeAccessed(receive)
            .Returns(true);
        sutProvider.GetDependency<IUploadReceiveFileCommand>()
            .GetUploadUrlAsync(receive, Arg.Any<string>(), Arg.Any<long>(), Arg.Any<string>())
            .Returns((expectedUrl, "generatedFileId"));

        var result = await sutProvider.Sut.GetReceiveFileUploadUrl(id, _uploadRequest);

        Assert.IsType<ReceiveFileUploadDataResponseModel>(result);
        Assert.Equal(expectedUrl, result.Url);
    }
}
