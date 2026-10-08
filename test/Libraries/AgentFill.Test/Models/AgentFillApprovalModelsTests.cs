using Bit.AgentFill.Entities;
using Bit.AgentFill.Models;
using Bit.Core.Exceptions;
using Xunit;

namespace Bit.AgentFill.Test.Models;

public class AgentFillApprovalModelsTests
{
    private static readonly DateTime _now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void From_Unanswered_BeforeExpiry_IsPending()
    {
        var model = ApprovalRecordResponseModel.From(Request(null, _now.AddMinutes(1)), _now);

        Assert.Equal("pending", model.Status);
    }

    [Fact]
    public void From_Unanswered_AtOrAfterExpiry_IsExpired()
    {
        Assert.Equal("expired", ApprovalRecordResponseModel.From(Request(null, _now), _now).Status);
        Assert.Equal("expired", ApprovalRecordResponseModel.From(Request(null, _now.AddMinutes(-1)), _now).Status);
    }

    [Fact]
    public void From_Answered_IsAnsweredEvenAfterExpiry()
    {
        var model = ApprovalRecordResponseModel.From(Request("sealed-response", _now.AddMinutes(-1)), _now);

        Assert.Equal("answered", model.Status);
    }

    [Fact]
    public void From_CopiesEveryStoredField()
    {
        var request = Request("sealed-response", _now.AddMinutes(1));
        request.ResponseDeviceId = Guid.NewGuid();
        request.ResponseDate = _now;

        var model = ApprovalRecordResponseModel.From(request, _now);

        Assert.Equal(request.Id, model.Id);
        Assert.Equal(request.RequestDeviceId, model.RequestDeviceId);
        Assert.Equal(request.SealedRequest, model.SealedRequest);
        Assert.Equal(request.SealedResponse, model.SealedResponse);
        Assert.Equal(request.ResponseDeviceId, model.ResponseDeviceId);
        Assert.Equal(request.CreationDate, model.CreationDate);
        Assert.Equal(request.ExpirationDate, model.ExpirationDate);
        Assert.Equal(request.ResponseDate, model.ResponseDate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_Missing_Throws(string? value)
    {
        Assert.Throws<BadRequestException>(() => SealedField.Validate(value, "SealedRequest"));
    }

    [Fact]
    public void Validate_AtTheLimit_ReturnsTheValue()
    {
        var value = new string('a', SealedField.MaxBytes);

        Assert.Same(value, SealedField.Validate(value, "SealedRequest"));
    }

    [Fact]
    public void Validate_OverTheLimit_Throws()
    {
        Assert.Throws<BadRequestException>(() =>
            SealedField.Validate(new string('a', SealedField.MaxBytes + 1), "SealedRequest"));
    }

    [Fact]
    public void Validate_CountsUtf8Bytes()
    {
        // 'é' is two bytes in UTF-8, so half the limit in characters is exactly the limit in bytes.
        Assert.Throws<BadRequestException>(() =>
            SealedField.Validate(new string('é', SealedField.MaxBytes / 2 + 1), "SealedRequest"));
    }

    private static AgentFillApprovalRequest Request(string? sealedResponse, DateTime expirationDate) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        RequestDeviceId = Guid.NewGuid(),
        SealedRequest = "sealed-request",
        SealedResponse = sealedResponse,
        CreationDate = expirationDate.AddMinutes(-5),
        ExpirationDate = expirationDate,
    };
}
