using Bit.Pam.Models;

namespace Bit.Services.Pam.Rotation.Models;

/// <summary>
/// A rotation config's detail view: its <see cref="PamRotationConfigDetails"/> projection together with every job
/// recorded against it (each carrying its own attempts, oldest first) — the read model for
/// <c>GET configs/{id}</c>.
/// </summary>
public sealed record PamRotationConfigHistory(
    PamRotationConfigDetails Config,
    IReadOnlyList<PamRotationJobDetails> Jobs);
