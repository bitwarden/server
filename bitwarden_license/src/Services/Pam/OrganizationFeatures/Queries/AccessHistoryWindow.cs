using Bit.Core.Exceptions;

namespace Bit.Services.Pam.OrganizationFeatures.Queries;

/// <summary>
/// The retention window every PAM history read goes through.
/// </summary>
public static class AccessHistoryWindow
{
    /// <summary>
    /// How far back a history read reaches.
    /// </summary>
    public const int RetentionDays = 90;

    /// <summary>
    /// Clamps a caller-supplied range to the window. A missing bound reaches as far as the window allows and an
    /// inverted pair is swapped.
    /// </summary>
    /// <exception cref="BadRequestException">The requested span is wider than the retention window.</exception>
    public static (DateTime Since, DateTime Until) ResolveRange(DateTime? start, DateTime? end, DateTime now)
    {
        var retentionFloor = now.AddDays(-RetentionDays);
        var since = start ?? retentionFloor;
        var until = end ?? now;

        if (since > until)
        {
            (since, until) = (until, since);
        }

        if (until - since > TimeSpan.FromDays(RetentionDays))
        {
            throw new BadRequestException(
                $"The requested range is wider than the {RetentionDays}-day audit retention window.");
        }

        // The outer clamp: no parameter reaches past retention, whatever the span between the two bounds.
        return (since < retentionFloor ? retentionFloor : since, until);
    }
}
