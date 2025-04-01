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
        (AccessEventLogs, Claims.AccessEventLogs),
        (AccessImportExport, Claims.AccessImportExport),
        (AccessReports, Claims.AccessReports),
        (CreateNewCollections, Claims.CreateNewCollections),
        (EditAnyCollection, Claims.EditAnyCollection),
        (DeleteAnyCollection, Claims.DeleteAnyCollection),
        (ManageGroups, Claims.ManageGroups),
        (ManagePolicies, Claims.ManagePolicies),
        (ManageSso, Claims.ManageSso),
        (ManageUsers, Claims.ManageUsers),
        (ManageResetPassword, Claims.ManageResetPassword),
        (ManageScim, Claims.ManageScim),
    };
}
