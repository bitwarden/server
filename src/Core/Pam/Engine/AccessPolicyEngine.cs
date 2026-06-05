using System.Globalization;
using System.Net;
using Bit.Core.Pam.Models.Rules;

namespace Bit.Core.Pam.Engine;

/// <summary>
/// Recursively evaluates the polymorphic <see cref="Rule"/> tree against the caller's signals. Each leaf rule
/// yields an <see cref="AccessDecision"/>; <see cref="AllOfRule"/> combines its children with deny taking
/// precedence over a pending approval, which in turn takes precedence over allow. Unparseable inputs fail closed.
/// </summary>
public sealed class AccessPolicyEngine : IAccessPolicyEngine
{
    // The rule JSON encodes days as the lowercase three-letter abbreviations the validator accepts.
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

    public AccessDecision Evaluate(Rule rule, AccessPolicySignals signals) => rule switch
    {
        HumanApprovalRule => AccessDecision.RequiresApproval,
        IpAllowlistRule ip => EvaluateIpAllowlist(ip, signals),
        TimeOfDayRule time => EvaluateTimeOfDay(time, signals),
        AllOfRule all => AccessDecision.Combine(all.Rules.Select(child => Evaluate(child, signals))),
        // A rule kind the engine does not understand cannot be shown to be satisfied, so deny.
        _ => AccessDecision.Deny(DenyReason.UnsupportedRule),
    };

    private static AccessDecision EvaluateIpAllowlist(IpAllowlistRule rule, AccessPolicySignals signals)
    {
        // An allowlist with no entries permits no address; combined with an unknown caller IP, both fail closed.
        if (rule.Cidrs.Count == 0 || signals.IpAddress is null)
        {
            return AccessDecision.Deny(DenyReason.NotWithinIpRange);
        }

        foreach (var cidr in rule.Cidrs)
        {
            if (IPNetwork.TryParse(cidr, out var network) && network.Contains(signals.IpAddress))
            {
                return AccessDecision.Allow;
            }
        }

        return AccessDecision.Deny(DenyReason.NotWithinIpRange);
    }

    private static AccessDecision EvaluateTimeOfDay(TimeOfDayRule rule, AccessPolicySignals signals)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(rule.Tz, out var timeZone))
        {
            // The window cannot be evaluated without a valid timezone, so fail closed.
            return AccessDecision.Deny(DenyReason.NotWithinTimeWindow);
        }

        var local = TimeZoneInfo.ConvertTime(signals.Timestamp, timeZone);
        var day = local.DayOfWeek;
        var time = TimeOnly.FromTimeSpan(local.TimeOfDay);

        foreach (var window in rule.Windows)
        {
            if (WindowContains(window, day, time))
            {
                return AccessDecision.Allow;
            }
        }

        return AccessDecision.Deny(DenyReason.NotWithinTimeWindow);
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
