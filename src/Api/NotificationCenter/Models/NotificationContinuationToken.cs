#nullable enable
using Bit.Core.NotificationCenter.Enums;

namespace Bit.Api.NotificationCenter.Models;

public class NotificationContinuationToken
{
    public Priority Priority { get; set; }

    public DateTime Date { get; set; }
}
