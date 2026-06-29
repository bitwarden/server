using System.Text.Json;
using Bit.HttpExtensions;

namespace Bit.Commercial.Pam.Api.Models.Response;

public class AccessRuleResponseModel : ResponseModel
{
    public AccessRuleResponseModel()
        : base("accessRule")
    {
    }

    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool Enabled { get; set; }
    public JsonElement? Conditions { get; set; }
    public bool SingleActiveLease { get; set; }
    public int? DefaultLeaseDurationSeconds { get; set; }
    public int? MaxLeaseDurationSeconds { get; set; }
    public bool AllowsExtensions { get; set; }
    public int? MaxExtensionDurationSeconds { get; set; }
    public IEnumerable<Guid> Collections { get; set; } = null!;
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }
}
