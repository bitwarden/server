using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Services.Pam.Api.Models.Request;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Models.Request;

public class AccessRequestHistoryRequestModelTests
{
    [Fact]
    public void ToQueryOptions_Empty_ReadsTheFirstPageAtTheDefaultSize()
    {
        var options = new AccessRequestHistoryRequestModel().ToQueryOptions();

        Assert.Equal(0, options.Page);
        Assert.Equal(AccessRequestHistoryRequestModel.DefaultPageSize, options.PageSize);
        Assert.Empty(options.Statuses);
        Assert.Empty(options.CollectionIds);
        Assert.Empty(options.RequesterIds);
    }

    [Fact]
    public void ToQueryOptions_StatusNames_MapToStatuses()
    {
        var options = new AccessRequestHistoryRequestModel
        {
            Status = ["denied", "revoked", "canceled", "expired", "sessionEnded"],
        }.ToQueryOptions();

        Assert.Equal(
            [
                AccessRequestHistoryStatus.Denied,
                AccessRequestHistoryStatus.Revoked,
                AccessRequestHistoryStatus.Canceled,
                AccessRequestHistoryStatus.Expired,
                AccessRequestHistoryStatus.SessionEnded,
            ],
            options.Statuses);
    }

    [Theory]
    [InlineData(null, 0, "unknown")]
    [InlineData(-1, null, null)]
    [InlineData(null, 0, null)]
    [InlineData(null, 101, null)]
    [InlineData(int.MaxValue, null, null)]
    public void ToQueryOptions_OutOfRange_ThrowsBadRequest(int? page, int? pageSize, string? status)
    {
        var model = new AccessRequestHistoryRequestModel
        {
            Page = page,
            PageSize = pageSize,
            Status = status == null ? null : [status],
        };

        Assert.Throws<BadRequestException>(() => model.ToQueryOptions());
        Assert.NotEmpty(model.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(model)));
    }
}
