using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Enums;

namespace Bit.Api.Billing.Models.Requests;

public class ChangePlanFrequencyRequest
{
    [Required]
    public PlanType NewPlanType { get; set; }
}
