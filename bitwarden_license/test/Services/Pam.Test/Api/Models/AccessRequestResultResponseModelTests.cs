using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Services.Pam.Api.Models.Response;
using Bit.Services.Pam.Enums;
using Bit.Services.Pam.Models;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Models;

public class AccessRequestResultResponseModelTests
{
    private static readonly DateTime _now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_AutomaticApproval_IsApprovedWithTheAutomaticDecision()
    {
        var request = OpenRequest(AccessRequestAction.Approved);
        var decision = new AccessDecision
        {
            Id = Guid.NewGuid(),
            AccessRequestId = request.Id,
            DeciderKind = AccessDeciderKind.Automatic,
            Verdict = AccessDecisionVerdict.Approve,
            CreationDate = _now,
        };

        var model = new AccessRequestResultResponseModel(AccessRequestResult.Automatic(request, decision), _now);

        Assert.Equal(AccessApprovalMode.Automatic, model.ApprovalMode);
        Assert.Equal(request.Id, model.Request.Id);
        Assert.Equal(AccessRequestStatus.Approved, model.Request.Status);
        var projected = Assert.Single(model.Request.Decisions);
        Assert.Equal(AccessDeciderKind.Automatic, projected.DeciderKind);
        Assert.Equal(AccessDecisionVerdict.Approve, projected.Verdict);
        Assert.Null(projected.Id);
        Assert.Null(projected.Comment);
        Assert.Equal(_now, projected.DecidedAt);
        // No lease is minted at submit; the requester activates the approved request.
        Assert.Null(model.Request.ProducedLeaseId);
        Assert.Null(model.Request.ProducedLeaseStatus);
    }

    [Fact]
    public void Constructor_HumanApproval_IsPendingWithNoDecisions()
    {
        var request = OpenRequest(AccessRequestAction.None);

        var model = new AccessRequestResultResponseModel(AccessRequestResult.Human(request), _now);

        Assert.Equal(AccessApprovalMode.Human, model.ApprovalMode);
        Assert.Equal(request.Id, model.Request.Id);
        Assert.Equal(request.CipherId, model.Request.CipherId);
        Assert.Equal(request.Reason, model.Request.Reason);
        Assert.Equal(AccessRequestStatus.Pending, model.Request.Status);
        Assert.Empty(model.Request.Decisions);
        Assert.Null(model.Request.ProducedLeaseId);
    }

    [Fact]
    public void Constructor_NullResult_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AccessRequestResultResponseModel(null!, _now));
    }

    private static AccessRequest OpenRequest(AccessRequestAction action) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        CollectionId = Guid.NewGuid(),
        CipherId = Guid.NewGuid(),
        RequesterId = Guid.NewGuid(),
        NotBefore = _now,
        NotAfter = _now.AddHours(1),
        Reason = "Rotate the database credentials",
        Action = action,
        CreationDate = _now,
        ActionDate = action == AccessRequestAction.None ? null : _now,
    };
}
