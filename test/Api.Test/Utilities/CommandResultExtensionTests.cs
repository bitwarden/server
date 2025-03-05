using Bit.Api.Models.CommandResults;
using Bit.Api.Utilities;
using Bit.Core.Models.Commands;
using Bit.Core.Vault.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Bit.Api.Test.Utilities;

public class CommandResultExtensionTests
{
    public static IEnumerable<object[]> TestCases()
    {
        yield return new object[]
        {
            new NoRecordFoundFailure<Cipher>(new[] { "Error 1", "Error 2" }),
            new ObjectResult(new[] { "Error 1", "Error 2" }) { StatusCode = StatusCodes.Status404NotFound }
        };
        yield return new object[]
        {
            new BadRequestFailure<Cipher>("Error 3"),
            new ObjectResult(new[] { "Error 3" }) { StatusCode = StatusCodes.Status400BadRequest }
        };
        yield return new object[]
        {
            new FailureCommandResult<Cipher>("Error 4"),
            new ObjectResult(new[] { "Error 4" }) { StatusCode = StatusCodes.Status400BadRequest }
        };
        var cipher = new Cipher() { Id = Guid.NewGuid() };

        yield return new object[]
        {
            new SuccessCommandResult<Cipher>(cipher),
            new ObjectResult(cipher) { StatusCode = StatusCodes.Status200OK }
        };
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void MapToActionResult_ShouldMapToHttpResponse(CommandResult<Cipher> input, ObjectResult expected)
    {
        var result = input.MapToActionResult();

        Assert.Equivalent(expected, result);
    }

    [Fact]
    public void MapToActionResult_ShouldThrowExceptionForUnhandledCommandResult()
    {
        var result = new NotImplementedCommandResult<Cipher>();

        Assert.Throws<InvalidOperationException>(() => result.MapToActionResult());
    }
}

public class NotImplementedCommandResult<T> : CommandResult<T>
{

}
