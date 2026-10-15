namespace Bit.Pam.Enums;

/// <summary>What caused a <see cref="Entities.PamRotationJob"/> to be offered.</summary>
public enum PamRotationSource : byte
{
    /// <summary>The config's <c>ScheduleCron</c> came due.</summary>
    Scheduled = 0,

    /// <summary>An admin triggered a rotation now.</summary>
    OnDemand = 1,

    /// <summary>A lease on the config's cipher ended and <c>RotateOnAccessEnd</c> is set.</summary>
    AccessEnd = 2,
}
