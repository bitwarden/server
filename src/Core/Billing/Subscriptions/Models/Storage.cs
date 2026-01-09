using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using OneOf;

namespace Bit.Core.Billing.Subscriptions.Models;

public record Storage
{
    private const double _bytesPerGibibyte = 1073741824D;

    /// <summary>
    /// The amount of storage the subscriber has available.
    /// </summary>
    public required short Available { get; init; }

    /// <summary>
    /// The amount of storage the subscriber has used.
    /// </summary>
    public required double Used { get; init; }

    /// <summary>
    /// The amount of storage the subscriber has used, formatted as a human-readable string.
    /// </summary>
    public required string ReadableUsed { get; init; }

    public static implicit operator Storage(User user) => From(user);
    public static implicit operator Storage(Organization organization) => From(organization);

    private static Storage From(OneOf<User, Organization> subscriber)
    {
        var maxStorageGB = subscriber.Match(
            user => user.MaxStorageGb,
            organization => organization.MaxStorageGb);

        if (maxStorageGB == null)
        {
            return null!;
        }

        var storage = subscriber.Match(
            user => user.Storage,
            organization => organization.Storage);

        return new Storage
        {
            Available = maxStorageGB.Value,
            Used = Math.Round((storage ?? 0) / _bytesPerGibibyte, 2),
            ReadableUsed = CoreHelpers.ReadableBytesSize(storage ?? 0)
        };
    }
}
