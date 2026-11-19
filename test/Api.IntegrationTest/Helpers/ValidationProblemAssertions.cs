using System.Net;
using System.Text.Json;
using Xunit;

namespace Bit.Api.IntegrationTest.Helpers;

public static class ValidationProblemAssertions
{
    /// <summary>
    /// Asserts that the response is an RFC 7807 problem with the given status, whose first error for
    /// <paramref name="propertyName"/> carries the given <paramref name="code"/> as its type.
    /// </summary>
    public static async Task AssertValidationProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string propertyName,
        string code)
    {
        Assert.Equal(status, response.StatusCode);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errorCode = problem.RootElement
            .GetProperty("errors")
            .GetProperty(propertyName)[0]
            .GetProperty("type")
            .GetString();
        Assert.Equal(code, errorCode);
    }

    /// <summary>
    /// Asserts that the response is a 404 RFC 7807 problem with the top-level type <c>not_found</c>, whose first
    /// error for <paramref name="propertyName"/> carries the given <paramref name="code"/> as its type.
    /// </summary>
    public static async Task AssertNotFoundProblemAsync(HttpResponseMessage response, string propertyName, string code)
    {
        await AssertValidationProblemAsync(response, HttpStatusCode.NotFound, propertyName, code);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("not_found", problem.RootElement.GetProperty("type").GetString());
    }
}
