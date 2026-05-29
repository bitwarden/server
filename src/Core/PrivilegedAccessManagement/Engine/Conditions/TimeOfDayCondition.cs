namespace Bit.Core.PrivilegedAccessManagement.Engine.Conditions;

public sealed class TimeOfDayCondition : IAccessCondition
{
    public AccessDecision Evaluate(AccessRuleEngineContext context)
    {
        var config = context.Rule.TimeOfDay;
        if (config is null)
        {
            return AccessDecision.Allow;
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(config.TimeZone, out var timeZone))
        {
            // The window cannot be evaluated without a valid timezone, so fail closed
            return AccessDecision.Deny(DenyReason.NotWithinTimeWindow);
        }

        var local = TimeZoneInfo.ConvertTime(context.Signals.UserTime, timeZone);
        var day = local.DayOfWeek;
        var time = TimeOnly.FromTimeSpan(local.TimeOfDay);

        foreach (var window in config.Windows)
        {
            var dayMatches = window.Days.Count == 0 || window.Days.Contains(day);
            if (dayMatches && time >= window.From && time <= window.To)
            {
                return AccessDecision.Allow;
            }
        }

        return AccessDecision.Deny(DenyReason.NotWithinTimeWindow);
    }
}
