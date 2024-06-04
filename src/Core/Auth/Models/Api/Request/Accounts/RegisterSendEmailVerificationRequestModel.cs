using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Models.Api.Request.Accounts;

public class RegisterSendEmailVerificationRequestModel
{
    [StringLength(50)]
    public string Name { get; set; }
    [Required]
    [StrictEmailAddress]
    [StringLength(256)]
    public string Email { get; set; }
    public bool ReceiveMarketingEmails { get; set; } = false;
}
