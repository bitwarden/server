using System.Text.Json.Serialization;

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
    public List<(bool Permission, string ClaimName)> ClaimsMap =>
        new()
        {
            (AccessEventLogs, "accesseventlogs"),
            (AccessImportExport, "accessimportexport"),
            (AccessReports, "accessreports"),
            (CreateNewCollections, "createnewcollections"),
            (EditAnyCollection, "editanycollection"),
            (DeleteAnyCollection, "deleteanycollection"),
            (ManageGroups, "managegroups"),
            (ManagePolicies, "managepolicies"),
            (ManageSso, "managesso"),
            (ManageUsers, "manageusers"),
            (ManageResetPassword, "manageresetpassword"),
            (ManageScim, "managescim"),
        };
}
