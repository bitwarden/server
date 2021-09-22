using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bit.Core.Models.Data 
{
    public class Permissions
    {
        public bool AccessBusinessPortal { get; set; }
        public bool AccessEventLogs { get; set; }
        public bool AccessImportExport { get; set; }
        public bool AccessReports { get; set; }
        [Obsolete("This permission exists for client backwards-compatibility. It should not be used to determine permissions in this repository", false)]
        public bool ManageAllCollections => CreateNewCollections && EditAnyCollection && DeleteAnyCollection;
        public bool CreateNewCollections { get; set; }
        public bool EditAnyCollection { get; set; }
        public bool DeleteAnyCollection { get; set; }
        [Obsolete("This permission exists for client backwards-compatibility. It should not be used to determine permissions in this repository", false)]
        public bool ManageAssignedCollections => EditAssignedCollections && DeleteAssignedCollections;
        public bool EditAssignedCollections { get; set; }
        public bool DeleteAssignedCollections { get; set; }
        public bool ManageGroups { get; set; }
        public bool ManagePolicies { get; set; }
        public bool ManageSso { get; set; }
        public bool ManageUsers { get; set; }
        public bool ManageResetPassword { get; set; }

        [JsonIgnore]
        public List<(Func<bool> Permission, string ClaimName)> ClaimsMap => new()
        {
            (() => AccessBusinessPortal, "accessbusinessportal"),
            (() => AccessEventLogs, "accesseventlogs"),
            (() => AccessImportExport, "accessimportexport"),
            (() => AccessReports, "accessreports"),
            (() => CreateNewCollections, "createnewcollections"),
            (() => EditAnyCollection, "editanycollection"),
            (() => DeleteAnyCollection, "deleteanycollection"),
            (() => EditAssignedCollections, "editassignedcollections"),
            (() => DeleteAssignedCollections, "deleteassignedcollections"),
            (() => ManageGroups, "managegroups"),
            (() => ManagePolicies, "managepolicies"),
            (() => ManageSso, "managesso"),
            (() => ManageUsers, "manageusers"),
            (() => ManageResetPassword, "manageresetpassword"),
        };
    }
}
