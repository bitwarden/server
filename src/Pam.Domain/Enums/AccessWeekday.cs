using System.Text.Json.Serialization;
using Bit.Pam.Models.Conditions;

namespace Bit.Pam.Enums;

/// <summary>
/// A day of the week used in a <see cref="Models.Conditions.TimeOfDayCondition"/> window. Values align with
/// <see cref="System.DayOfWeek"/> (Sunday = 0) so the engine can compare directly. Serialized as the lowercase
/// three-letter tokens (<c>"sun".."sat"</c>) via <see cref="AccessWeekdayJsonConverter"/>.
/// </summary>
[JsonConverter(typeof(AccessWeekdayJsonConverter))]
public enum AccessWeekday : byte
{
    Sun = 0,
    Mon = 1,
    Tue = 2,
    Wed = 3,
    Thu = 4,
    Fri = 5,
    Sat = 6,
}
