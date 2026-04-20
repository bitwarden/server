namespace Bit.Seeder.Pipeline;

/// <summary>
/// Events emitted by the seeder pipeline for progress reporting.
/// Consumed via <see cref="System.IProgress{T}"/> to keep the library UI-agnostic.
/// </summary>
public abstract record SeederProgressEvent(string Phase);

/// <summary>Emitted when a phase begins. <paramref name="Total"/> is null for indeterminate work.</summary>
public sealed record PhaseStarted(string Phase, int? Total) : SeederProgressEvent(Phase);

/// <summary>Emitted periodically as a phase makes progress. <paramref name="Delta"/> is incremental, not cumulative.</summary>
public sealed record PhaseAdvanced(string Phase, int Delta) : SeederProgressEvent(Phase);

/// <summary>Emitted when a phase completes.</summary>
public sealed record PhaseCompleted(string Phase) : SeederProgressEvent(Phase);
