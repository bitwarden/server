using Bit.Pam.Enums;
using Bit.Services.Pam.Api.Models.Response;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Models;

/// <summary>
/// The wire vocabulary is a contract with the web client, which keeps its own copy in
/// <c>bitwarden_license/bit-web/src/app/pam/access-audit/responses/access-audit-event.response.ts</c>. A kind added
/// here and not there renders as "Unknown event" in the audit trail, which is how the rotation and fleet kinds
/// shipped (PM-43606). If the pinned list below fails, update the client enum, its label map, and the copy in
/// <c>apps/web/src/locales/en/messages.json</c> in the same change.
/// </summary>
public class AccessAuditEventKindNamesTests
{
    private static readonly string[] _vocabulary =
    [
        "requestSubmitted",
        "requestApproved",
        "requestDenied",
        "requestCancelled",
        "requestExpiredUnanswered",
        "requestExpiredUnactivated",
        "leaseActivated",
        "leaseActivationRejected",
        "leaseExtended",
        "leaseRevoked",
        "leaseExpired",
        "credentialAccessed",
        "credentialAccessDenied",
        "ruleCreated",
        "ruleUpdated",
        "ruleDeleted",
        "leasingKillSwitchTriggered",
        "leasingFreezeEnabled",
        "leasingFreezeLifted",
        "rotationConfigCreated",
        "rotationSettingsUpdated",
        "rotationAccountUpdated",
        "rotationPaused",
        "rotationResumed",
        "rotationConfigDeleted",
        "rotationOffered",
        "rotationDispatched",
        "rotationSucceeded",
        "rotationAttemptFailed",
        "rotationFailed",
        "rotationJobReleased",
        "rotationJobTimedOut",
        "rotationCipherWriteRejected",
        "rotationReportRejected",
        "manualRotationDue",
        "manualRotationRecorded",
        "daemonRegistered",
        "daemonRevoked",
        "daemonDisabled",
        "daemonEnabled",
        "daemonDeleted",
        "daemonAssignedToTarget",
        "daemonUnassignedFromTarget",
        "targetSystemRegistered",
        "targetSystemDisabled",
        "targetSystemEnabled",
        "targetSystemRenamed",
        "targetSystemPolicyUpdated",
        "targetSystemDeleted",
    ];

    [Fact]
    public void From_ReportsExactlyThePinnedVocabulary()
    {
        var reported = Enum.GetValues<AccessAuditEventKind>().Select(AccessAuditEventKindNames.From).Order();

        Assert.Equal(_vocabulary.Order(), reported);
    }

    [Fact]
    public void From_NamesEveryKindDistinctly()
    {
        var kinds = Enum.GetValues<AccessAuditEventKind>();

        Assert.Equal(kinds.Length, kinds.Select(AccessAuditEventKindNames.From).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void TryParse_ReadsBackEveryNameFromReports()
    {
        foreach (var kind in Enum.GetValues<AccessAuditEventKind>())
        {
            Assert.True(AccessAuditEventKindNames.TryParse(AccessAuditEventKindNames.From(kind), out var parsed));
            Assert.Equal(kind, parsed);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("RequestSubmitted")]
    [InlineData("somethingAddedLater")]
    public void TryParse_RefusesANameItDoesNotReport(string name)
    {
        Assert.False(AccessAuditEventKindNames.TryParse(name, out _));
    }
}
