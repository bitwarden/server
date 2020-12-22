using System.Collections.Generic;
using Bit.Core.Models.Api;
using Bit.Core.Models.Interfaces;
using System.Linq;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Business
{
    public class OrganizationUserInvite: IPermissions
    {
        public IEnumerable<string> Emails { get; set; }
        public Enums.OrganizationUserType? Type { get; set; }
        public bool AccessBusinessPortal { get; set; }
        public bool AccessEventLogs { get; set; }
        public bool AccessImportExport { get; set; }
        public bool AccessReports { get; set; }
        public bool ManageAllCollections { get; set; }
        public bool ManageAssignedCollections { get; set; }
        public bool ManageGroups { get; set; }
        public bool ManagePolicies { get; set; }
        public bool ManageUsers { get; set; }
        public bool AccessAll { get; set; }
        public IEnumerable<SelectionReadOnly> Collections { get; set; }

        public OrganizationUserInvite() {}

        public OrganizationUserInvite(OrganizationUserInviteRequestModel requestModel) 
        {
            Emails = requestModel.Emails;
            Type = requestModel.Type.Value;
            AccessAll = requestModel.AccessAll;
            Collections = requestModel.Collections.Select(c => c.ToSelectionReadOnly());
            AccessBusinessPortal = requestModel.AccessBusinessPortal;
            AccessEventLogs = requestModel.AccessEventLogs;
            AccessImportExport = requestModel.AccessImportExport;
            AccessReports = requestModel.AccessReports;
            ManageAllCollections = requestModel.ManageAllCollections;
            ManageAssignedCollections = requestModel.ManageAssignedCollections;
            ManageGroups = requestModel.ManageGroups;
            ManagePolicies = requestModel.ManagePolicies;
            ManageUsers = requestModel.ManageUsers;
        }
    }
}
