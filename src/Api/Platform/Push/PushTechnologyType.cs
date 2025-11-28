using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Enums;

public enum PushTechnologyType
{
    [Display(Name = "SignalR")]
    SignalR = 0,
    [Display(Name = "WebPush")]
    WebPush = 1,
}
