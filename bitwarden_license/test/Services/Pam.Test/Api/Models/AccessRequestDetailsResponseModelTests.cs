using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;
using Xunit;
using ApiEnums = Bit.Services.Pam.Api.Models;

namespace Bit.Services.Pam.Test.Api.Models;

public class AccessRequestDetailsResponseModelTests
{
    [Fact]
    public void Ctor_MarksTimestampsAsUtc()
    {
        // Regression guard: the approver inbox drops a request whose requested window has lapsed. When the stored
        // UTC instants are serialised without a 'Z' (Kind=Unspecified), a client east of UTC reparses them as local
        // time and the shift hides still-valid requests. The model must relabel the kind as UTC.
        var unspecified = new DateTime(2026, 6, 15, 13, 0, 0, DateTimeKind.Unspecified);
        var details = new AccessRequestDetails
        {
            Status = AccessRequestStatus.Pending,
            NotBefore = unspecified,
            NotAfter = unspecified.AddHours(1),
            CreationDate = unspecified.AddMinutes(-5),
            ResolvedDate = unspecified.AddMinutes(10),
        };

        var model = new AccessRequestDetailsResponseModel(details);

        Assert.Equal(DateTimeKind.Utc, model.LeaseNotBefore.Kind);
        Assert.Equal(DateTimeKind.Utc, model.LeaseNotAfter.Kind);
        Assert.Equal(DateTimeKind.Utc, model.SubmittedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, model.ResolvedAt!.Value.Kind);
        // SpecifyKind relabels without shifting the wall clock.
        Assert.Equal(unspecified.Ticks, model.LeaseNotBefore.Ticks);
    }

    [Fact]
    public void Ctor_LeavesNullResolvedDateNull()
    {
        var unspecified = new DateTime(2026, 6, 15, 13, 0, 0, DateTimeKind.Unspecified);
        var details = new AccessRequestDetails
        {
            Status = AccessRequestStatus.Pending,
            NotBefore = unspecified,
            NotAfter = unspecified.AddHours(1),
            CreationDate = unspecified,
            ResolvedDate = null,
        };

        var model = new AccessRequestDetailsResponseModel(details);

        Assert.Null(model.ResolvedAt);
    }

    [Fact]
    public void Ctor_MapsHumanApproverAsSingleApproversElement()
    {
        // The requester's own request list names who decided the request; the resolved approver identity, verdict, and
        // comment must flow through as a single Approvers element rather than being dropped (which would leave the
        // client showing a raw id). The array shape future-proofs the contract for multi-party approval.
        var approverId = Guid.NewGuid();
        var unspecified = new DateTime(2026, 6, 15, 13, 0, 0, DateTimeKind.Unspecified);
        var decidedAt = unspecified.AddMinutes(20);
        var details = new AccessRequestDetails
        {
            Status = AccessRequestStatus.Denied,
            NotBefore = unspecified,
            NotAfter = unspecified.AddHours(1),
            CreationDate = unspecified,
            ResolvedDate = decidedAt,
            Decisions =
            [
                new AccessRequestDecision
                {
                    DeciderKind = AccessDeciderKind.Human,
                    Id = approverId,
                    Name = "Ada Approver",
                    Email = "ada@example.com",
                    Comment = "Outside approved hours",
                    Verdict = AccessDecisionVerdict.Deny,
                    DecidedAt = decidedAt,
                },
            ],
        };

        var model = new AccessRequestDetailsResponseModel(details);

        var decision = Assert.Single(model.Decisions);
        Assert.Equal(ApiEnums.DeciderKind.Human, decision.DeciderKind);
        Assert.Equal(approverId, decision.Id!.Value);
        Assert.Equal("Ada Approver", decision.Name);
        Assert.Equal("ada@example.com", decision.Email);
        Assert.Equal("Outside approved hours", decision.Comment);
        Assert.Equal(ApiEnums.AccessDecisionVerdict.Deny, decision.Verdict);
        Assert.Equal(decidedAt.Ticks, decision.DecidedAt.Ticks);
        Assert.Equal(DateTimeKind.Utc, decision.DecidedAt.Kind);
    }

    [Fact]
    public void Ctor_MapsAutomaticDecisionWithNoApproverIdentity()
    {
        // An automatic (access-rule) decision is surfaced like any other, but with an Automatic decider kind and no
        // approver identity — the client renders it as a rule-driven decision rather than a person.
        var unspecified = new DateTime(2026, 6, 15, 13, 0, 0, DateTimeKind.Unspecified);
        var details = new AccessRequestDetails
        {
            Status = AccessRequestStatus.Approved,
            NotBefore = unspecified,
            NotAfter = unspecified.AddHours(1),
            CreationDate = unspecified,
            ResolvedDate = unspecified.AddMinutes(1),
            Decisions =
            [
                new AccessRequestDecision
                {
                    DeciderKind = AccessDeciderKind.Automatic,
                    Id = null,
                    Verdict = AccessDecisionVerdict.Approve,
                    DecidedAt = unspecified.AddMinutes(1),
                },
            ],
        };

        var model = new AccessRequestDetailsResponseModel(details);

        var decision = Assert.Single(model.Decisions);
        Assert.Equal(ApiEnums.DeciderKind.Automatic, decision.DeciderKind);
        Assert.Null(decision.Id);
        Assert.Null(decision.Name);
        Assert.Equal(ApiEnums.AccessDecisionVerdict.Approve, decision.Verdict);
    }

    [Fact]
    public void Ctor_PassesThroughPinnedRuleIdAndLeavesItNullWhenUnpinned()
    {
        // The governing rule pinned at submit flows through verbatim; a request created before pinning existed has no
        // pinned rule and must report null rather than a value fabricated from a re-resolution. ExpiredAt stays null
        // (no backing store in v1).
        var ruleId = Guid.NewGuid();
        var unspecified = new DateTime(2026, 6, 15, 13, 0, 0, DateTimeKind.Unspecified);
        var details = new AccessRequestDetails
        {
            Status = AccessRequestStatus.Pending,
            NotBefore = unspecified,
            NotAfter = unspecified.AddHours(1),
            CreationDate = unspecified,
        };

        Assert.Null(new AccessRequestDetailsResponseModel(details).RuleId);

        details.RuleId = ruleId;
        var model = new AccessRequestDetailsResponseModel(details);

        Assert.Equal(ruleId, model.RuleId);
        Assert.Null(model.ExpiredAt);
    }

    [Fact]
    public void Ctor_LeavesDecisionsEmptyWhenPending()
    {
        // A pending request has no decision recorded yet, so the decision log is empty.
        var unspecified = new DateTime(2026, 6, 15, 13, 0, 0, DateTimeKind.Unspecified);
        var details = new AccessRequestDetails
        {
            Status = AccessRequestStatus.Pending,
            NotBefore = unspecified,
            NotAfter = unspecified.AddHours(1),
            CreationDate = unspecified,
        };

        var model = new AccessRequestDetailsResponseModel(details);

        Assert.Empty(model.Decisions);
    }
}
