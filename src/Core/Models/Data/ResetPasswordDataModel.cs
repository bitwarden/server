using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Data
{
    public class ResetPasswordDataModel
    {
        [Display(Name = "ResetPasswordAutoEnrollCheckbox")]
        public bool AutoEnrollEnabled { get; set; }
    }
}
