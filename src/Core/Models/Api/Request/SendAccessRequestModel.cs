using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Models.Api
{
    public class SendAccessRequestModel
    {
        [StringLength(300)]
        public string Password { get; set; }
    }
}
