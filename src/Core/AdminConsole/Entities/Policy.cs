using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Entities;

/// <summary>
/// An organization policy of a given <see cref="PolicyType"/>, optionally with additional
/// configuration data. Policies enforce rules on members of the organization or change organization behavior.
/// </summary>
/// <remarks>
/// The policy domain has various business rules to determine whether a policy should be enforced against a user.
/// You should not read the Policy object directly to decide this. Instead, see <see cref="IPolicyRequirementQuery"/>.
/// </remarks>
public class Policy : ITableObject<Guid>
{
    /// <summary>
    /// A unique identifier for the policy.
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// The ID of the <see cref="Organization"/> this policy belongs to.
    /// </summary>
    public Guid OrganizationId { get; set; }
    /// <summary>
    /// The type of policy, which determines its description and behavior.
    /// </summary>
    public PolicyType Type { get; set; }
    /// <summary>
    /// Optional JSON configuration for the policy. The shape of the data depends on the
    /// <see cref="Type"/>. Use <see cref="GetDataModel{T}"/> and <see cref="SetDataModel{T}"/>
    /// to read and write this field.
    /// </summary>
    public string? Data { get; set; }
    /// <summary>
    /// If true, the policy is turned on and may be enforced (subject to other business rules such as
    /// exemptions or plan limitations). If false, the policy exists and may be configured but has no effect.
    /// </summary>
    public bool Enabled { get; set; }
    /// <summary>
    /// The date the policy was created.
    /// </summary>
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    /// <summary>
    /// The date the policy was last updated.
    /// </summary>
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

    /// <summary>
    /// Initializes <see cref="Id"/> to a new COMB GUID.
    /// </summary>
    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }

    /// <summary>
    /// Deserializes <see cref="Data"/> into the specified <see cref="IPolicyDataModel"/> type.
    /// </summary>
    public T GetDataModel<T>() where T : IPolicyDataModel, new()
    {
        return CoreHelpers.LoadClassFromJsonData<T>(Data);
    }

    /// <summary>
    /// Serializes the specified <see cref="IPolicyDataModel"/> and stores it in <see cref="Data"/>.
    /// </summary>
    public void SetDataModel<T>(T dataModel) where T : IPolicyDataModel, new()
    {
        Data = CoreHelpers.ClassToJsonData(dataModel);
    }
}
