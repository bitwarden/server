using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.HttpExtensions.Test;

/// <summary>
/// The whole document, not one field at a time. Everything else asserts the piece it cares about, which cannot
/// catch a member appearing, disappearing, or moving — and the document is the contract every client parses, so a
/// change to it is a breaking change however the C# side is spelled. Read these as the wire format's spec.
/// </summary>
public class ValidationProblemDocumentTests
{
    [Fact]
    public async Task SeveralPropertiesFailingAtOnce()
    {
        var result = TypedResults.BitwardenValidationProblem(errors: new[]
        {
            ("reason", new ErrorCode("required", "Reason is required.")),
            ("name", new ErrorCode("too_long", "Name must be 200 characters or shorter.",
                new JsonObject { ["max"] = 200 })),
            ("duration", new ErrorCode("must_be_positive", "Duration must be greater than zero.")),
        });

        Assert.Equal(
            """
            {
              "type": "validation_error",
              "title": "One or more validation errors occurred.",
              "status": 400,
              "errors": {
                "reason": [
                  {
                    "type": "required",
                    "detail": "Reason is required."
                  }
                ],
                "name": [
                  {
                    "type": "too_long",
                    "detail": "Name must be 200 characters or shorter.",
                    "parameters": {
                      "max": 200
                    }
                  }
                ],
                "duration": [
                  {
                    "type": "must_be_positive",
                    "detail": "Duration must be greater than zero."
                  }
                ]
              }
            }
            """,
            await ExecuteAsync(result));
    }

    [Fact]
    public async Task ConflictCarriedInTheSameBodyOnADifferentStatus()
    {
        var result = TypedResults.BitwardenValidationProblem(
            errors: new[] { ("code", new ErrorCode("already_active", "You already have active access to this item.")) },
            title: "The request conflicts with the current state.",
            type: "conflict_error",
            statusCode: StatusCodes.Status409Conflict);

        Assert.Equal(
            """
            {
              "type": "conflict_error",
              "title": "The request conflicts with the current state.",
              "status": 409,
              "errors": {
                "code": [
                  {
                    "type": "already_active",
                    "detail": "You already have active access to this item."
                  }
                ]
              }
            }
            """,
            await ExecuteAsync(result));
    }

    [Fact]
    public async Task SeveralFailuresOnOneProperty()
    {
        var result = TypedResults.BitwardenValidationProblem(errors: new[]
        {
            ("password", new ErrorCode("too_short", "Password must be at least 12 characters.",
                new JsonObject { ["min"] = 12 })),
            ("password", new ErrorCode("invalid", "Password is not valid.")),
        });

        Assert.Equal(
            """
            {
              "type": "validation_error",
              "title": "One or more validation errors occurred.",
              "status": 400,
              "errors": {
                "password": [
                  {
                    "type": "too_short",
                    "detail": "Password must be at least 12 characters.",
                    "parameters": {
                      "min": 12
                    }
                  },
                  {
                    "type": "invalid",
                    "detail": "Password is not valid."
                  }
                ]
              }
            }
            """,
            await ExecuteAsync(result));
    }

    [Fact]
    public async Task ANestedModelAndACollectionElement()
    {
        var result = TypedResults.BitwardenValidationProblem(errors: new[]
        {
            ("owner.email", new ErrorCode("invalid_email", "Email is not an address.")),
            ("members[1].name", new ErrorCode("too_long", "Name must be 200 characters or shorter.",
                new JsonObject { ["max"] = 200 })),
        });

        Assert.Equal(
            """
            {
              "type": "validation_error",
              "title": "One or more validation errors occurred.",
              "status": 400,
              "errors": {
                "owner.email": [
                  {
                    "type": "invalid_email",
                    "detail": "Email is not an address."
                  }
                ],
                "members[1].name": [
                  {
                    "type": "too_long",
                    "detail": "Name must be 200 characters or shorter.",
                    "parameters": {
                      "max": 200
                    }
                  }
                ]
              }
            }
            """,
            await ExecuteAsync(result));
    }

    private static async Task<string> ExecuteAsync(IResult result)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider(),
        };
        using var stream = new MemoryStream();
        context.Response.Body = stream;

        await result.ExecuteAsync(context);

        stream.Position = 0;
        return Indent(await new StreamReader(stream).ReadToEndAsync());
    }

    private static string Indent(string json) => JsonSerializer.Serialize(
        JsonSerializer.Deserialize<JsonElement>(json), new JsonSerializerOptions { WriteIndented = true });
}
