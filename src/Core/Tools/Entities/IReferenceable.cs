#nullable enable
using Bit.Core.Tools.Models.Business;

namespace Bit.Core.Tools.Entities;

/// <summary>
/// An entity that can be referenced by a <see cref="ReferenceEvent"/>.
/// </summary>
public interface IReferenceable
{
    /// <summary>
    /// Identifies the entity that generated the event.
    /// </summary>
    Guid Id { get; set; }

    /// <summary>
    /// Contextual information included in the event.
    /// </summary>
    /// <remarks>
    /// Do not store secrets in this field.
    /// </remarks>
    string? ReferenceData { get; set; }

    /// <summary>
    /// Returns <see langword="true" /> when the entity is a user.
    /// Otherwise returns <see langword="false" />.
    /// </summary>
    bool IsUser();
}
