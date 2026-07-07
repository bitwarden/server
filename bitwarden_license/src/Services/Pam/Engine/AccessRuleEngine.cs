using System.Globalization;
using System.Net;
using Bit.Services.Pam.Models.Conditions;

namespace Bit.Services.Pam.Engine;

/// <summary>
/// Evaluates the access rule's flat list of <see cref="AccessCondition"/>s against the caller's signals. Each
/// condition yields an <see cref="AccessEvaluation"/>; the results combine with deny taking precedence over a
/// pending approval, which in turn takes precedence over allow. An empty list is vacuously satisfied (allow).
/// Unparseable inputs fail closed before they reach the engine.
/// </summary>
public sealed class AccessRuleEngine : IAccessRuleEngine
{
    public AccessEvaluation Evaluate(IReadOnlyList<AccessCondition> conditions, AccessSignals signals) =>
        AccessEvaluation.Combine(conditions.Select(condition => EvaluateCondition(condition, signals)));

    private static AccessEvaluation EvaluateCondition(AccessCondition condition, AccessSignals signals) => condition switch
    {
        HumanApprovalCondition => AccessEvaluation.RequiresApproval,
        IpAllowlistCondition ip => EvaluateIpAllowlist(ip, signals),
        TimeOfDayCondition time => EvaluateTimeOfDay(time, signals),
        // A condition kind the engine does not understand cannot be shown to be satisfied, so deny.
        _ => AccessEvaluation.Deny(DenyReason.UnsupportedCondition),
    };

    private static AccessEvaluation EvaluateIpAllowlist(IpAllowlistCondition condition, AccessSignals signals)
    {
        // An allowlist with no entries permits no address; combined with an unknown caller IP, both fail closed.
        if (condition.Cidrs.Count == 0 || signals.IpAddress is null)
        {
            return AccessEvaluation.Deny(DenyReason.NotWithinIpRange);
        }

        foreach (var cidr in condition.Cidrs)
        {
            if (IPNetwork.TryParse(cidr, out var network) && network.Contains(signals.IpAddress))
            {
                return AccessEvaluation.Allow;
            }
        }

        return AccessEvaluation.Deny(DenyReason.NotWithinIpRange);
    }

    private static AccessEvaluation EvaluateTimeOfDay(TimeOfDayCondition condition, AccessSignals signals)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(condition.Tz, out var timeZone))
        {
            // The window cannot be evaluated without a valid timezone, so fail closed.
            return AccessEvaluation.Deny(DenyReason.NotWithinTimeWindow);
        }

        var local = TimeZoneInfo.ConvertTime(signals.Timestamp, timeZone);
        var day = local.DayOfWeek;
        var time = TimeOnly.FromTimeSpan(local.TimeOfDay);

        foreach (var window in condition.Windows)
        {
            if (WindowContains(window, day, time))
            {
                return AccessEvaluation.Allow;
            }
        }

        return AccessEvaluation.Deny(DenyReason.NotWithinTimeWindow);
    }

    private static bool WindowContains(TimeWindow window, DayOfWeek day, TimeOnly time)
    {
        // AccessWeekday values align with System.DayOfWeek (Sunday = 0), so a direct cast compares correctly.
        var dayMatches = window.Days.Any(d => (DayOfWeek)d == day);
        if (!dayMatches)
        {
            return false;
        }

        return TimeOnly.TryParseExact(window.From, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
            && TimeOnly.TryParseExact(window.To, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to)
            && time >= from && time <= to;
    }
}
