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
}
