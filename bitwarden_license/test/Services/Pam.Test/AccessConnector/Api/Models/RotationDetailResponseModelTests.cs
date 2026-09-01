using System.Text.Json;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.AccessConnector.Api.Models.Response;
using Bit.Services.Pam.AccessConnector.Models;
using Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Api.Models;

/// <summary>
/// Locks the wire shape of the two rotation detail reads: the subject's fields on the response itself, not nested
/// under a property, and the history under the timestamp names the server emits. Property lookup on the client is
/// insensitive to the first character's case, so these assertions are about the names and the nesting, not the
/// casing.
/// </summary>
public class RotationDetailResponseModelTests
{
    private static readonly DateTime _created = new(2026, 8, 25, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _resolved = new(2026, 8, 25, 9, 5, 0, DateTimeKind.Utc);

    [Fact]
    public void DaemonDetail_CarriesTheDaemonFieldsAndItsActivityOnOneObject()
    {
        var daemon = new PamDaemon
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Name = "rotation-daemon-1",
            Status = PamAccessConnectorStatus.Enabled,
            LastHeartbeatAt = _created,
            CreationDate = _created,
            RevisionDate = _created,
        };
        var targetSystemId = Guid.NewGuid();
        var history = new PamAccessConnectorHistory(
            new PamAccessConnectorListItem(daemon, IsConnected: true, [targetSystemId]),
            [Job(daemon.Id)]);

        var json = Serialize(new PamAccessConnectorDetailResponseModel(history));

        Assert.Equal(daemon.Id, json.GetProperty("Id").GetGuid());
        Assert.Equal(daemon.Name, json.GetProperty("Name").GetString());
        Assert.True(json.GetProperty("IsConnected").GetBoolean());
        Assert.Equal(targetSystemId, json.GetProperty("AssignedTargetSystemIds")[0].GetGuid());
        AssertJobShape(json);
    }

    [Fact]
    public void ConfigDetail_CarriesTheConfigFieldsAndItsHistoryOnOneObject()
    {
        var config = new PamRotationConfigDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            CipherId = Guid.NewGuid(),
            TargetSystemId = Guid.NewGuid(),
            TargetSystemName = "prod-mssql",
            TargetSystemMethod = PamTargetSystemMethod.Automatic,
            AccountIdentity = "svc-account",
            CreationDate = _created,
            RevisionDate = _created,
        };
        var history = new PamRotationConfigHistory(config, [Job(Guid.NewGuid())]);

        var json = Serialize(new PamRotationConfigDetailResponseModel(history, awaitingManualRotation: false));

        Assert.Equal(config.Id, json.GetProperty("Id").GetGuid());
        Assert.Equal(config.TargetSystemName, json.GetProperty("TargetSystemName").GetString());
        Assert.Equal(config.AccountIdentity, json.GetProperty("AccountIdentity").GetString());
        AssertJobShape(json);
    }

    private static void AssertJobShape(JsonElement json)
    {
        var job = Assert.Single(json.GetProperty("Jobs").EnumerateArray().ToList());
        Assert.Equal(_created, job.GetProperty("CreationDate").GetDateTime());

        var attempt = Assert.Single(job.GetProperty("Attempts").EnumerateArray().ToList());
        Assert.Equal(_created, attempt.GetProperty("CreationDate").GetDateTime());
        Assert.Equal(_resolved, attempt.GetProperty("ResolvedDate").GetDateTime());
    }

    private static PamRotationJobDetails Job(Guid daemonId)
    {
        var job = new PamRotationJob
        {
            Id = Guid.NewGuid(),
            RotationConfigId = Guid.NewGuid(),
            Source = PamRotationSource.Scheduled,
            Status = PamRotationJobStatus.Succeeded,
            CreationDate = _created,
            NextClaimableAt = _created,
            ExpiresAt = _resolved,
        };
        return PamRotationJobDetails.From(job, [
            new PamRotationAttempt
            {
                Id = Guid.NewGuid(),
                JobId = job.Id,
                ClaimedByDaemonId = daemonId,
                CipherUpdated = true,
                Status = PamRotationAttemptStatus.Rotated,
                SessionTermination = PamSessionTerminationOutcome.Terminated,
                CreationDate = _created,
                ResolvedDate = _resolved,
            }
        ]);
    }

    private static JsonElement Serialize<T>(T model) =>
        JsonDocument.Parse(JsonSerializer.Serialize(model)).RootElement;
}
