using Bit.Pam.Models;

namespace Bit.Services.Pam.AccessConnector.Models;

/// <summary>
/// A rotation config's detail view: the config plus every job recorded against it, newest first, each with its
/// attempts oldest first.
/// </summary>
public sealed record PamRotationConfigHistory(
    PamRotationConfigDetails Config,
    IReadOnlyList<PamRotationJobDetails> Jobs);
