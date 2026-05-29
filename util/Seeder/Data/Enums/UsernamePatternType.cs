namespace Bit.Seeder.Data.Enums;

/// <summary>
/// Username/email format patterns used by organizations.
/// </summary>
public enum UsernamePatternType
{
    /// <summary>
    /// first.last@domain.com
    /// </summary>
    FirstDotLast,

    /// <summary>
    /// f.last@domain.com
    /// </summary>
    FDotLast,

    /// <summary>
    /// flast@domain.com
    /// </summary>
    FLast,

    /// <summary>
    /// last.first@domain.com
    /// </summary>
    LastDotFirst,

    /// <summary>
    /// first_last@domain.com
    /// </summary>
    First_Last,

    /// <summary>
    /// lastf@domain.com
    /// </summary>
    LastFirst
}
