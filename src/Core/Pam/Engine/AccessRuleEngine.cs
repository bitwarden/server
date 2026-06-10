using System.Globalization;
using System.Net;
using Bit.Core.Pam.Models.Conditions;

namespace Bit.Core.Pam.Engine;

/// <summary>
/// Recursively evaluates the polymorphic <see cref="AccessCondition"/> tree against the caller's signals. Each leaf
/// condition yields an <see cref="AccessEvaluation"/>; <see cref="AllOfCondition"/> combines its children with deny
/// taking precedence over a pending approval, which in turn takes precedence over allow. Unparseable inputs fail
/// closed.
/// </summary>
public sealed class AccessRuleEngine : IAccessRuleEngine
{
    // The conditions JSON encodes days as the lowercase three-letter abbreviations the validator accepts.
    private static readonly IReadOnlyDictionary<string, DayOfWeek> _days = new Dictionary<string, DayOfWeek>(StringComparer.OrdinalIgnoreCase)
    {
        ["sun"] = DayOfWeek.Sunday,
        ["mon"] = DayOfWeek.Monday,
        ["tue"] = DayOfWeek.Tuesday,
        ["wed"] = DayOfWeek.Wednesday,
        ["thu"] = DayOfWeek.Thursday,
        ["fri"] = DayOfWeek.Friday,
        ["sat"] = DayOfWeek.Saturday,
    };

    public AccessEvaluation Evaluate(AccessCondition condition, AccessSignals signals) => condition switch
    {
        HumanApprovalCondition => AccessEvaluation.RequiresApproval,
        IpAllowlistCondition ip => EvaluateIpAllowlist(ip, signals),
        TimeOfDayCondition time => EvaluateTimeOfDay(time, signals),
        AllOfCondition all => AccessEvaluation.Combine(all.Conditions.Select(child => Evaluate(child, signals))),
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
        var dayMatches = window.Days.Any(d => _days.TryGetValue(d, out var parsed) && parsed == day);
        if (!dayMatches)
        {
            return false;
        }

        return TimeOnly.TryParseExact(window.From, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
            && TimeOnly.TryParseExact(window.To, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to)
            && time >= from && time <= to;
    }
}
