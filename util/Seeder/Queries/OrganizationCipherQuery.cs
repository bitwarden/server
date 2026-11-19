using System.ComponentModel.DataAnnotations;
using Bit.Core.Vault.Repositories;

namespace Bit.Seeder.Queries;

/// <summary>
/// Reads a cipher's stored type, raw data JSON, item key, and revision date, so a test can assert on what a client
/// wrote.
/// </summary>
public class OrganizationCipherQuery(ICipherRepository cipherRepository)
    : IQuery<OrganizationCipherQuery.Request, OrganizationCipherQuery.Response>
{
    public class Request
    {
        [Required]
        public required Guid CipherId { get; set; }
    }

    public class Response
    {
        public required Guid CipherId { get; set; }
        public required int Type { get; set; }
        public required string Data { get; set; }
        public string? Key { get; set; }
        public required DateTime RevisionDate { get; set; }
    }

    public async Task<Response> Execute(Request request)
    {
        var cipher = await cipherRepository.GetByIdAsync(request.CipherId)
            ?? throw new InvalidOperationException($"Cipher {request.CipherId} not found.");

        return new Response
        {
            CipherId = cipher.Id,
            Type = (int)cipher.Type,
            Data = cipher.Data ?? string.Empty,
            Key = cipher.Key,
            RevisionDate = cipher.RevisionDate
        };
    }
}
