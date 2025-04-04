#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bit.Admin.Billing.Models;

public class BusinessUnitConversionModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Provider Admin Email")]
    public string? ProviderAdminEmail { get; set; }

    [BindNever]
    public required Organization Organization { get; set; }

    [BindNever]
    public Guid? ProviderId { get; set; }

    [BindNever]
    public string? Success { get; set; }

    [BindNever] public List<string>? Errors { get; set; } = [];
}
