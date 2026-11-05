using System.ComponentModel.DataAnnotations;
using Bit.Pam.Repositories;

namespace Bit.Seeder.Queries;

/// <summary>
/// Reads a PAM access connector's status and last heartbeat, so a test can assert it was enrolled and polled.
/// </summary>
public class PamAccessConnectorStateQuery(IPamAccessConnectorRepository accessConnectorRepository)
    : IQuery<PamAccessConnectorStateQuery.Request, PamAccessConnectorStateQuery.Response>
{
    public class Request
    {
        [Required]
        public required Guid AccessConnectorId { get; set; }
    }

    public class Response
    {
        public required Guid AccessConnectorId { get; set; }
        public required int Status { get; set; }
        public DateTime? LastHeartbeatDate { get; set; }
    }

    public async Task<Response> Execute(Request request)
    {
        var accessConnector = await accessConnectorRepository.GetByIdAsync(request.AccessConnectorId)
            ?? throw new InvalidOperationException($"Access connector {request.AccessConnectorId} not found.");

        return new Response
        {
            AccessConnectorId = accessConnector.Id,
            Status = (int)accessConnector.Status,
            LastHeartbeatDate = accessConnector.LastHeartbeatDate
        };
    }
}
