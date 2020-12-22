using System;
using Bit.Core.Models.Interfaces;

namespace Bit.Core.Models.Data
{
    public class OrganizationUserOrganizationDetails: IPermissions
    {
        public Guid OrganizationId { get; set; }
        public Guid? UserId { get; set; }
        public string Name { get; set; }
        public bool UsePolicies { get; set; }
        public bool UseSso { get; set; }
        public bool UseGroups { get; set; }
        public bool UseDirectory { get; set; }
        public bool UseEvents { get; set; }
        public bool UseTotp { get; set; }
        public bool Use2fa { get; set; }
        public bool UseApi{ get; set; }
        public bool UseBusinessPortal => UsePolicies || UseSso;
        public bool SelfHost { get; set; }
        public bool UsersGetPremium { get; set; }
        public int Seats { get; set; }
        public int MaxCollections { get; set; }
        public short? MaxStorageGb { get; set; }
        public string Key { get; set; }
        public Enums.OrganizationUserStatusType Status { get; set; }
        public Enums.OrganizationUserType Type { get; set; }
        public bool Enabled { get; set; }
        public string SsoExternalId { get; set; }
        public string Identifier { get; set; }
        public bool AccessBusinessPortal { get; set; }
        public bool AccessEventLogs { get; set; }
        public bool AccessImportExport { get; set; }
        public bool AccessReports { get; set; }
        public bool ManageAllCollections { get; set; }
        public bool ManageAssignedCollections { get; set; }
        public bool ManageGroups { get; set; }
        public bool ManagePolicies { get; set; }
        public bool ManageUsers { get; set; }
    }
}
