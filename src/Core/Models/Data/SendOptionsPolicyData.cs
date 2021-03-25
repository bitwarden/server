using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Data
{
    public class SendOptionsPolicyData
    {
        [Display(Name = "DisableHideEmail")]
        public bool DisableHideEmail { get; set; }
    }
}
