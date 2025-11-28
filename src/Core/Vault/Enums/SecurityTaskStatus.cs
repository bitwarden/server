using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Vault.Enums;

public enum SecurityTaskStatus : byte
{
    /// <summary>
    /// Default status for newly created tasks that have not been completed.
    /// </summary>
    [Display(Name = "Pending")]
    Pending = 0,

    /// <summary>
    /// Status when a task is considered complete and has no remaining actions
    /// </summary>
    [Display(Name = "Completed")]
    Completed = 1,
}
