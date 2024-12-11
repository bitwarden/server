#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Models.Api.Request.Accounts;

public class RegisterSendVerificationEmailRequestModel
{
    [StringLength(50)]
    public string? Name { get; set; }

    [StrictEmailAddress]
    [StringLength(256)]
    public required string Email { get; set; }
    public bool ReceiveMarketingEmails { get; set; }
}
