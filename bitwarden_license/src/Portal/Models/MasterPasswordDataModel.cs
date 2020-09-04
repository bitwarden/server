using System.ComponentModel.DataAnnotations;

namespace Bit.Portal.Models
{
    public class MasterPasswordDataModel
    {
        [Display(Name = "MinimumLength")]
        [Range(8, int.MaxValue, ErrorMessage = "MasterPasswordMinLengthError")]
        public int? MinLength { get; set; }
        [Display(Name = "MinimumComplexityScore")]
        public int? MinComplexity { get; set; }
        [Display(Name = "UppercaseAZ")]
        public bool RequireUpper { get; set; }
        [Display(Name = "LowercaseAZ")]
        public bool RequireLower { get; set; }
        [Display(Name = "Numbers09")]
        public bool RequireNumbers { get; set; }
        [Display(Name = "SpecialCharacters")]
        public bool RequireSpecial { get; set; }
    }
}
