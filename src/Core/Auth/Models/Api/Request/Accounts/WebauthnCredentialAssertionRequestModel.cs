using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Auth.Models.Api.Request.Accounts;

public class WebauthnCredentialAssertionRequestModel
{
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; }
}

