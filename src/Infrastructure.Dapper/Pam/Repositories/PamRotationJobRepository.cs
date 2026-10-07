using System.Data;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Infrastructure.Dapper.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Pam.Repositories;

public class PamRotationJobRepository : BaseRepository, IPamRotationJobRepository
{
    public PamRotationJobRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public PamRotationJobRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<PamRotationJobCreateOutcome> CreateGuardedAsync(PamRotationJob job)
    {
        await using var connection = new SqlConnection(ConnectionString);
        // Passed straight through: the properties match the procedure's parameters, including a plain input @Id.
        var result = await connection.ExecuteScalarAsync<int>(
            "[dbo].[PamRotationJob_Create]",
            job,
            commandType: CommandType.StoredProcedure);

        return (PamRotationJobCreateOutcome)result;
    }

    public async Task<PamRotationJob?> GetByIdAsync(Guid id)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<PamRotationJob>(
            "[dbo].[PamRotationJob_ReadById]",
            new { Id = id },
            commandType: CommandType.StoredProcedure);

        return results.SingleOrDefault();
    }

    public async Task<PamRotationClaimResult> ClaimAsync(
        Guid jobId,
        Guid accessConnectorId,
        DateTime now,
        TimeSpan releaseDelay)
    {
        await using var connection = new SqlConnection(ConnectionString);
        // The procedure always returns one row, with columns matching PamRotationClaimResult.
        return await connection.QuerySingleAsync<PamRotationClaimResult>(
            "[dbo].[PamRotationJob_Claim]",
            new
            {
                JobId = jobId,
                AttemptId = CombGuid.Generate(),
                AccessConnectorId = accessConnectorId,
                Now = now,
                ReleaseDelaySeconds = (int)releaseDelay.TotalSeconds,
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<ICollection<PamClaimableJob>> GetManyClaimableByAccessConnectorIdAsync(
        Guid accessConnectorId,
        DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<PamClaimableJob>(
            "[dbo].[PamRotationJob_ReadManyClaimableByAccessConnectorId]",
            new { AccessConnectorId = accessConnectorId, Now = now },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task<ICollection<PamRotationJobDetails>> GetManyByConfigIdAsync(Guid configId, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        using var results = await connection.QueryMultipleAsync(
            "[dbo].[PamRotationJob_ReadManyByConfigId]",
            new { RotationConfigId = configId },
            commandType: CommandType.StoredProcedure);

        var jobs = (await results.ReadAsync<PamRotationJob>()).ToList();
        var attemptsByJobId = (await results.ReadAsync<PamRotationAttempt>())
            .GroupBy(attempt => attempt.JobId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return jobs
            .Select(job => PamRotationJobDetails.From(
                job,
                attemptsByJobId.TryGetValue(job.Id, out var attempts) ? attempts : new List<PamRotationAttempt>(),
                now))
            .ToList();
    }

    public async Task<ICollection<PamRotationJobDetails>> GetManyRecentByAccessConnectorIdAsync(
        Guid accessConnectorId,
        int limit,
        DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        using var results = await connection.QueryMultipleAsync(
            "[dbo].[PamRotationJob_ReadManyRecentByAccessConnectorId]",
            new { AccessConnectorId = accessConnectorId, Limit = limit },
            commandType: CommandType.StoredProcedure);

        var jobs = (await results.ReadAsync<PamRotationJob>()).ToList();
        var attemptsByJobId = (await results.ReadAsync<PamRotationAttempt>())
            .GroupBy(attempt => attempt.JobId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return jobs
            .Select(job => PamRotationJobDetails.From(
                job,
                attemptsByJobId.TryGetValue(job.Id, out var attempts) ? attempts : new List<PamRotationAttempt>(),
                now))
            .ToList();
    }

    public async Task<PamRotationAttempt?> GetAttemptByIdAsync(Guid attemptId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<PamRotationAttempt>(
            "[dbo].[PamRotationAttempt_ReadById]",
            new { Id = attemptId },
            commandType: CommandType.StoredProcedure);

        return results.SingleOrDefault();
    }

    public async Task<PamRotationCipherWriteOutcome> AcceptCipherWriteAsync(Guid attemptId, Guid accessConnectorId, string cipherData,
        DateTime lastKnownRevisionDate, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var result = await connection.ExecuteScalarAsync<int>(
            "[dbo].[PamRotationAttempt_AcceptCipherWrite]",
            new
            {
                AttemptId = attemptId,
                AccessConnectorId = accessConnectorId,
                CipherData = cipherData,
                LastKnownRevisionDate = lastKnownRevisionDate,
                Now = now,
            },
            commandType: CommandType.StoredProcedure);

        return (PamRotationCipherWriteOutcome)result;
    }

    public async Task<PamRotationAttemptResolveOutcome> MarkAttemptRotatedAsync(Guid attemptId, Guid accessConnectorId,
        PamSessionTerminationOutcome sessionTermination, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var result = await connection.ExecuteScalarAsync<int>(
            "[dbo].[PamRotationAttempt_MarkRotated]",
            new
            {
                AttemptId = attemptId,
                AccessConnectorId = accessConnectorId,
                SessionTermination = (byte)sessionTermination,
                Now = now,
            },
            commandType: CommandType.StoredProcedure);

        return (PamRotationAttemptResolveOutcome)result;
    }

    public async Task<PamRotationFailureResult> MarkAttemptErroredAsync(Guid attemptId, Guid accessConnectorId, string? failureReason,
        PamRotationSyncState syncState, DateTime now, int maxAttempts, TimeSpan retryBaseDelay)
    {
        await using var connection = new SqlConnection(ConnectionString);
        // The Rejected row returns NULL for ErroredAttemptCount, which PamRotationFailureResult cannot hold.
        var row = await connection.QuerySingleAsync<MarkErroredRow>(
            "[dbo].[PamRotationAttempt_MarkErrored]",
            new
            {
                AttemptId = attemptId,
                AccessConnectorId = accessConnectorId,
                FailureReason = failureReason,
                SyncState = (byte)syncState,
                Now = now,
                MaxAttempts = maxAttempts,
                RetryBaseDelaySeconds = (int)retryBaseDelay.TotalSeconds,
            },
            commandType: CommandType.StoredProcedure);

        return new PamRotationFailureResult
        {
            Outcome = (PamRotationAttemptResolveOutcome)row.Outcome,
            JobStatus = row.JobStatus.HasValue ? (PamRotationJobStatus)row.JobStatus.Value : null,
            ErroredAttemptCount = row.ErroredAttemptCount ?? 0,
        };
    }

    public async Task<IReadOnlyList<PamTimedOutJob>> TimeoutDueAsync(DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<PamTimedOutJob>(
            "[dbo].[PamRotationJob_TimeoutDue]",
            new { Now = now },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task<IReadOnlyList<PamReleasedJob>> ReleaseExpiredLeasesAsync(DateTime now, TimeSpan offlineAfter,
        TimeSpan releaseDelay)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<PamReleasedJob>(
            "[dbo].[PamRotationJob_ReleaseExpiredLeases]",
            new
            {
                Now = now,
                OfflineAfterSeconds = (int)offlineAfter.TotalSeconds,
                ReleaseDelaySeconds = (int)releaseDelay.TotalSeconds,
            },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    private sealed class MarkErroredRow
    {
        public int Outcome { get; set; }
        public byte? JobStatus { get; set; }
        public int? ErroredAttemptCount { get; set; }
    }
}
