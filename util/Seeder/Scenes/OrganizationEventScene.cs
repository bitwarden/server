using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class OrganizationEventScene(
    IOrganizationRepository organizationRepository,
    IEventRepository eventRepository,
    IManglerService manglerService) : IScene<OrganizationEventScene.Request, OrganizationEventScene.Result>
{
    private const int MaxEventCount = 20;

    public class Request
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        public EventType Type { get; set; } = EventType.Organization_Updated;
        public int Count { get; set; } = 2;
        public Guid? ActingUserId { get; set; }
    }

    public class Result
    {
        public required int SeededCount { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        var organization = await organizationRepository.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            throw new InvalidOperationException($"Organization {request.OrganizationId} not found.");
        }

        if (request.Count < 1)
        {
            throw new InvalidOperationException($"Count must be at least 1, but was {request.Count}.");
        }

        if (request.Count > MaxEventCount)
        {
            throw new InvalidOperationException($"Count must not exceed {MaxEventCount}, but was {request.Count}.");
        }

        var baseDate = DateTime.UtcNow;
        var seededCount = 0;

        for (var i = 0; i < request.Count; i++)
        {
            var auditEvent = EventSeeder.Create(
                new EventSeed
                {
                    OrganizationId = organization.Id,
                    Type = request.Type,
                    Date = baseDate.AddSeconds(-i),
                    ActingUserId = request.ActingUserId
                });
            await eventRepository.CreateAsync(auditEvent);
            seededCount++;
        }

        return new SceneResult<Result>(
            result: new Result { SeededCount = seededCount },
            mangleMap: manglerService.GetMangleMap());
    }
}
