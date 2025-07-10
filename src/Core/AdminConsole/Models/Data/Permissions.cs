using System.Text.Json.Serialization;
using Bit.Core.Identity;

namespace Bit.Core.Models.Data;

public class Permissions
{
    public bool AccessEventLogs { get; set; }
    public bool AccessImportExport { get; set; }
    public bool AccessReports { get; set; }
    public bool CreateNewCollections { get; set; }
    public bool EditAnyCollection { get; set; }
    public bool DeleteAnyCollection { get; set; }
    public bool ManageGroups { get; set; }
    public bool ManagePolicies { get; set; }
    public bool ManageSso { get; set; }
    public bool ManageUsers { get; set; }
    public bool ManageResetPassword { get; set; }
    public bool ManageScim { get; set; }

    [JsonIgnore]
    public List<(bool Permission, string ClaimName)> ClaimsMap => new()
    {
        (AccessEventLogs, Claims.CustomPermissions.AccessEventLogs),
        (AccessImportExport, Claims.CustomPermissions.AccessImportExport),
        (AccessReports, Claims.CustomPermissions.AccessReports),
        (CreateNewCollections, Claims.CustomPermissions.CreateNewCollections),
        (EditAnyCollection, Claims.CustomPermissions.EditAnyCollection),
        (DeleteAnyCollection, Claims.CustomPermissions.DeleteAnyCollection),
        (ManageGroups, Claims.CustomPermissions.ManageGroups),
        (ManagePolicies, Claims.CustomPermissions.ManagePolicies),
        (ManageSso, Claims.CustomPermissions.ManageSso),
        (ManageUsers, Claims.CustomPermissions.ManageUsers),
        (ManageResetPassword, Claims.CustomPermissions.ManageResetPassword),
        (ManageScim, Claims.CustomPermissions.ManageScim),
    };
}
