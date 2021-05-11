using System.ComponentModel.DataAnnotations;

namespace Bit.Portal.Models
{
    public class ResetPasswordDataModel
    {
        [Display(Name = "ResetPasswordAutoEnrollCheckbox")]
        public bool AutoEnrollEnabled { get; set; }
    }
}
