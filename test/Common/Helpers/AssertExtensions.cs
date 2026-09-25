#nullable enable

using System.Text.Json;
using Xunit;

namespace Bit.Test.Common.Helpers;

public static class AssertExtensions
{
    extension(Assert)
    {
        public static async Task SuccessResponseAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync();
            var formatted = TryFormatJson(body) ?? body;

            Assert.Fail(
                $"Expected success, got {(int)response.StatusCode} {response.ReasonPhrase}.\n\n" +
                $"Response body:\n{formatted}");
        }
    }

    private static string? TryFormatJson(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }
        try
        {
            using var doc = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
