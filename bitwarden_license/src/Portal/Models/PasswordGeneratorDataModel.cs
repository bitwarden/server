using System.ComponentModel.DataAnnotations;

namespace Bit.Portal.Models
{
    public class PasswordGeneratorDataModel
    {
        // Shared
        [Display(Name = "MinimumLength")]
        [Range(5, 128)]
        public int? MinLength { get; set; }
        [Display(Name = "DefaultType")]
        public string DefaultType { get; set; }
        // PG - Password
        [Display(Name = "UppercaseAZ")]
        public bool UseUpper { get; set; }
        [Display(Name = "LowercaseAZ")]
        public bool UseLower { get; set; }
        [Display(Name = "Numbers09")]
        public bool UseNumbers { get; set; }
        [Display(Name = "SpecialCharacters")]
        public bool UseSpecial { get; set; }
        [Display(Name = "MinimumNumbers")]
        [Range(0, 9)]
        public int? MinNumbers { get; set; }
        [Display(Name = "MinimumSpecial")]
        [Range(0, 9)]
        public int? MinSpecial { get; set; }
        // PG - Passphrase
        [Display(Name = "MinimumNumberOfWords")]
        [Range(3, 20)]
        public int? MinNumberWords { get; set; }
        [Display(Name = "Capitalize")]
        public bool Capitalize { get; set; }
        [Display(Name = "IncludeNumber")]
        public bool IncludeNumber { get; set; }
    }
}
