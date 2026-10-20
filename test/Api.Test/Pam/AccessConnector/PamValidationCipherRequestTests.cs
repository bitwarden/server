#if !OSS
using Bit.Api.Vault.Models;
using Bit.Api.Vault.Models.Request;
using Bit.Core.Models.Api;
using Bit.Core.Vault.Enums;
using Bit.Services.Pam.Api.Endpoints.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace Bit.Api.Test.Pam.AccessConnector;

/// <summary>
/// The rotation cipher routes accept the standard cipher request, so the PAM validation filter must validate it and
/// the nested vault models like any other PAM request model.
/// </summary>
public class PamValidationCipherRequestTests
{
    private static string Enc(string label) =>
        $"2.{Convert.ToBase64String(new byte[16])}|{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(label))}|" +
        Convert.ToBase64String(new byte[32]);

    private static CipherRequestModel Model() => new()
    {
        Type = CipherType.Login,
        Name = Enc("name"),
        Login = new CipherLoginModel { Username = Enc("user"), Password = Enc("password") },
    };

    private static Task<object?> Run(CipherRequestModel model, out Func<bool> nextCalled)
    {
        var called = false;
        nextCalled = () => called;
        EndpointFilterDelegate next = _ =>
        {
            called = true;
            return ValueTask.FromResult<object?>("ok");
        };
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), [model]);
        return new PamValidationEndpointFilter().InvokeAsync(context, next).AsTask();
    }

    [Fact]
    public async Task InvokeAsync_ValidCipherRequest_CallsNext()
    {
        var result = await Run(Model(), out var nextCalled);

        Assert.True(nextCalled());
        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task InvokeAsync_CipherRequestWithInvalidTopLevelField_Returns400()
    {
        var model = Model();
        model.Name = "not an encrypted string";

        var result = await Run(model, out var nextCalled);

        Assert.False(nextCalled());
        var json = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, json.StatusCode);
        Assert.Contains(nameof(CipherRequestModel.Name), json.Value!.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task InvokeAsync_CipherRequestWithInvalidNestedField_Returns400()
    {
        var model = Model();
        model.Login!.Password = "plaintext";

        var result = await Run(model, out var nextCalled);

        Assert.False(nextCalled());
        var json = Assert.IsType<JsonHttpResult<ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, json.StatusCode);
        Assert.Contains(nameof(CipherLoginModel.Password), json.Value!.ValidationErrors!.Keys);
    }
}
#endif
