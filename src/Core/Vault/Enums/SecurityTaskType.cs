using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Vault.Enums;

public enum SecurityTaskType : byte
{
    /// <summary>
    /// Task to update a cipher's password that was found to be at-risk by an administrator
    /// </summary>
    [Display(Name = "Update at-risk credential")]
    UpdateAtRiskCredential = 0
}
