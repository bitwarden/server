using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Models.Request.Accounts;

public class DeleteRecoverRequestModel
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; }
}
