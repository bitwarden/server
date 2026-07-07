using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Microsoft.AspNetCore.Http.HttpResults.BitwardenTypedResultsExtensions;

namespace Bit.HttpExtensions.Test;

public class BitwardenValidationProblemResultTests
{
    [Fact]
    public void ExposesStatusCodeContentTypeAndProblemDetails()
    {
        var errors = new Dictionary<string, ErrorCode[]>
        {
            { "email", [new ErrorCode("invalid", "Email is invalid.")] }
        };

        var result = TypedResults.BitwardenValidationProblem(errors);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("application/problem+json", result.ContentType);
        Assert.NotNull(result.ProblemDetails);

        IValueHttpResult valueResult = result;
        IValueHttpResult<ProblemDetails> typedValueResult = result;
        Assert.Same(result.ProblemDetails, valueResult.Value);
        Assert.Same(result.ProblemDetails, typedValueResult.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WritesProblemDetailsToResponseBody()
    {
        var errors = new Dictionary<string, ErrorCode[]>
        {
            { "email", [new ErrorCode("memberNotClaimed", "Member not claimed")] }
        };
        var result = TypedResults.BitwardenValidationProblem(errors);

        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        using var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        await result.ExecuteAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);

        responseBody.Seek(0, SeekOrigin.Begin);
        using var document = await JsonDocument.ParseAsync(responseBody);
        var root = document.RootElement;
        Assert.Equal("One or more validation errors occurred.", root.GetProperty("title").GetString());
        Assert.Equal("validation_error", root.GetProperty("type").GetString());
        var responseErrors = root.GetProperty("errors");
        var emailErrors = responseErrors.GetProperty("email");
        var firstError = emailErrors[0];
        Assert.Equal("memberNotClaimed", firstError.GetProperty("type").GetString());
        Assert.Equal("Member not claimed", firstError.GetProperty("detail").GetString());
    }
}
