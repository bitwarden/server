using System.Text;
using System.Text.Json;
using Bit.Api.Tools.Controllers;
using Bit.Api.Tools.Models.Response;
using Bit.Core.Exceptions;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;
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
        receive.Data = JsonSerializer.Serialize(new ReceiveFileData("encrypted-name", "file.txt"));
        SetupHttpContext(sutProvider, CoreHelpers.Base64UrlEncode(Encoding.UTF8.GetBytes(receive.Secret)));
        sutProvider.GetDependency<IReceiveRepository>()
            .GetByIdAsync(receiveId)
            .Returns(receive);
        sutProvider.GetDependency<IReceiveAuthorizationService>()
            .ReceiveCanBeAccessed(receive)
            .Returns(true);

        var result = await sutProvider.Sut.GetShared(receiveId);

        Assert.IsType<SharedReceiveResponseModel>(result);
        var fileData = JsonSerializer.Deserialize<ReceiveFileData>(receive.Data);
        Assert.Equal(fileData!.Name, result.Name);
        Assert.Equal(receive.ScekWrappedPublicKey, result.ScekWrappedPublicKey);
    }
}
