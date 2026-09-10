namespace Bit.Pam.Enums;

/// <summary>
/// What caused a <see cref="Entities.PamRotationJob"/> to be offered.
/// </summary>
public enum PamRotationSource : byte
{
    /// <summary>The config's <c>ScheduleCron</c> came due (spec <c>RotationDue</c>).</summary>
    Scheduled = 0,

    /// <summary>An admin called <c>TriggerRotationNow</c>.</summary>
    OnDemand = 1,

    /// <summary>A lease on the config's cipher ended (revoke, self-end, or natural expiry) and <c>RotateOnAccessEnd</c> is set.</summary>
    AccessEnd = 2,
}
