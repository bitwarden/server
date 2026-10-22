using System.ComponentModel.DataAnnotations;
using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Request;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Models.Request;

public class AccessAuditTrailFilterRequestModelTests
{
    [Fact]
    public void ToQueryOptions_WithNothingSet_SelectsNothingAndBoundsNothing()
    {
        var options = new AccessAuditTrailFilterRequestModel().ToQueryOptions();

        Assert.Null(options.Start);
        Assert.Null(options.End);
        Assert.Empty(options.Kinds);
        Assert.Empty(options.ActorIds);
        Assert.False(options.IncludeAutomatedActor);
        Assert.Empty(options.RequesterIds);
        Assert.Empty(options.CipherIds);
        Assert.Empty(options.RuleIds);
        Assert.Null(options.Before);
    }

    [Fact]
    public void ToQueryOptions_ReadsEachKindOffTheGovernanceVocabulary()
    {
        var model = new AccessAuditTrailFilterRequestModel { Kind = ["requestApproved", "leaseRevoked"] };

        var options = model.ToQueryOptions();

        Assert.Equal(
            [AccessAuditEventKind.RequestApproved, AccessAuditEventKind.LeaseRevoked],
            options.Kinds);
        Assert.Empty(Validate(model));
    }

    [Theory]
    [InlineData("requestapproved")]
    [InlineData("RequestApproved")]
    [InlineData("somethingElse")]
    [InlineData("")]
    public void Validate_UnknownKind_IsRejected(string kind)
    {
        var model = new AccessAuditTrailFilterRequestModel { Kind = [kind] };

        var error = Assert.Single(Validate(model));

        Assert.Contains(nameof(AccessAuditTrailFilterRequestModel.Kind), error.MemberNames);
        Assert.Throws<BadRequestException>(() => model.ToQueryOptions());
    }

    [Fact]
    public void ToQueryOptions_CarriesBothHalvesOfAnItemSelection()
    {
        var cipherId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();

        var options = new AccessAuditTrailFilterRequestModel
        {
            CipherId = [cipherId],
            RuleId = [ruleId],
        }.ToQueryOptions();

        Assert.Equal([cipherId], options.CipherIds);
        Assert.Equal([ruleId], options.RuleIds);
    }

    [Fact]
    public void ToQueryOptions_ReadsAContinuationTokenBackIntoAPosition()
    {
        var occurredAt = new DateTime(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc);
        var id = Guid.NewGuid();
        var model = new AccessAuditTrailFilterRequestModel
        {
            ContinuationToken = $"{occurredAt.Ticks}_{id:N}",
        };

        var options = model.ToQueryOptions();

        Assert.Equal(new AccessAuditEventCursor(occurredAt, id), options.Before);
        Assert.Empty(Validate(model));
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("638000000000000000")]
    [InlineData("_0123456789abcdef0123456789abcdef")]
    [InlineData("638000000000000000_not-a-guid")]
    [InlineData("-1_0123456789abcdef0123456789abcdef")]
    public void Validate_AContinuationTokenThisEndpointDidNotIssue_IsRejected(string token)
    {
        var model = new AccessAuditTrailFilterRequestModel { ContinuationToken = token };

        var error = Assert.Single(Validate(model));

        Assert.Contains(nameof(AccessAuditTrailFilterRequestModel.ContinuationToken), error.MemberNames);
        Assert.Throws<BadRequestException>(() => model.ToQueryOptions());
    }

    // An unspecified kind is read as UTC; a local one is converted.
    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public void ToQueryOptions_NormalisesTheBoundsOntoUtc(DateTimeKind kind)
    {
        var wall = new DateTime(2026, 7, 3, 12, 0, 0);
        var start = DateTime.SpecifyKind(wall, kind);
        var expected = kind == DateTimeKind.Local
            ? start.ToUniversalTime()
            : DateTime.SpecifyKind(wall, DateTimeKind.Utc);

        var options = new AccessAuditTrailFilterRequestModel { Start = start, End = start.AddDays(1) }
            .ToQueryOptions();

        Assert.Equal(DateTimeKind.Utc, options.Start!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, options.End!.Value.Kind);
        Assert.Equal(expected, options.Start!.Value);
        Assert.Equal(expected.AddDays(1), options.End!.Value);
    }

    private static List<ValidationResult> Validate(AccessAuditTrailFilterRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
