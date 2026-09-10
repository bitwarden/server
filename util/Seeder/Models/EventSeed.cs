using Bit.Core.Enums;

namespace Bit.Seeder.Models;

/// <summary>
/// Input for <see cref="Factories.EventSeeder.Create"/>. A single audit event to seed for an organization.
/// </summary>
internal record EventSeed
{
    public required Guid OrganizationId { get; init; }
    public required EventType Type { get; init; }
    public required DateTime Date { get; init; }
    public Guid? ActingUserId { get; init; }
}
