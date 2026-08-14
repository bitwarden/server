#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request;

/// <summary>
/// Updates a collection's metadata alongside add/update/remove deltas for its user and group access.
/// </summary>
public class UpdateCollectionWithDeltaRequestModel
{
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string? Name { get; set; }

    [StringLength(300)]
    public string? ExternalId { get; set; }

    public CollectionUserAccessDeltaRequestModel Users { get; set; } = new();

    public CollectionGroupAccessDeltaRequestModel Groups { get; set; } = new();
}
