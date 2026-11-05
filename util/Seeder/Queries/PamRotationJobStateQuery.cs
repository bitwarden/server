using System.ComponentModel.DataAnnotations;
using Bit.Pam.Repositories;

namespace Bit.Seeder.Queries;

/// <summary>
/// Reads a PAM rotation job's stored state, its attempts (oldest first), and its config's last rotation date, so a
/// test can assert on what an access connector did.
/// </summary>
public class PamRotationJobStateQuery(
    IPamRotationJobRepository rotationJobRepository,
    IPamRotationConfigRepository rotationConfigRepository) : IQuery<PamRotationJobStateQuery.Request, PamRotationJobStateQuery.Response>
{
    // Generous cap on the config's jobs read back to find this job's attempts.
    private const int _jobReadLimit = 1000;

    public class Request
    {
        [Required]
        public required Guid JobId { get; set; }
    }

    public class Response
    {
        public required Guid JobId { get; set; }
        public required int Action { get; set; }
        public Guid? ClaimedByAccessConnectorId { get; set; }
        public DateTime? LastRotationDate { get; set; }
        public required List<AttemptResponse> Attempts { get; set; }
    }

    public class AttemptResponse
    {
        public required Guid AttemptId { get; set; }
        public required Guid ClaimedByAccessConnectorId { get; set; }
        public required int Action { get; set; }
        public required bool CipherUpdated { get; set; }
        public string? FailureReason { get; set; }
        public int? SyncState { get; set; }
        public int? SessionTermination { get; set; }
        public DateTime? ResolvedDate { get; set; }
    }

    public async Task<Response> Execute(Request request)
    {
        var job = await rotationJobRepository.GetByIdAsync(request.JobId)
            ?? throw new InvalidOperationException($"Rotation job {request.JobId} not found.");
        var config = await rotationConfigRepository.GetByIdAsync(job.RotationConfigId)
            ?? throw new InvalidOperationException($"Rotation config {job.RotationConfigId} not found.");

        var now = DateTime.UtcNow;
        var jobs = await rotationJobRepository.GetManyByConfigIdAsync(
            config.Id, _jobReadLimit, job.CreationDate.AddSeconds(-1), now);
        var attempts = jobs.SingleOrDefault(j => j.Id == job.Id)?.Attempts ?? [];

        return new Response
        {
            JobId = job.Id,
            Action = (int)job.Action,
            ClaimedByAccessConnectorId = job.ClaimedByAccessConnectorId,
            LastRotationDate = config.LastRotationDate,
            Attempts = attempts
                .OrderBy(a => a.CreationDate)
                .Select(a => new AttemptResponse
                {
                    AttemptId = a.Id,
                    ClaimedByAccessConnectorId = a.ClaimedByAccessConnectorId,
                    Action = (int)a.Action,
                    CipherUpdated = a.CipherUpdated,
                    FailureReason = a.FailureReason,
                    SyncState = (int?)a.SyncState,
                    SessionTermination = (int?)a.SessionTermination,
                    ResolvedDate = a.ResolvedDate
                })
                .ToList()
        };
    }
}
