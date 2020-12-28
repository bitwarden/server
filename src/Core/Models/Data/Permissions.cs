namespace Bit.Core.Models.Data 
{
    public class Permissions
    {
        public bool AccessBusinessPortal { get; set; }
        public bool AccessEventLogs { get; set; }
        public bool AccessImportExport { get; set; }
        public bool AccessReports { get; set; }
        public bool ManageAssignedCollections { get; set; }
        public bool ManageAllCollections { get; set; }
        public bool ManageGroups { get; set; }
        public bool ManagePolicies { get; set; }
        public bool ManageSso { get; set; }
        public bool ManageUsers { get; set; }
    }
}
