using System;
using Bit.Core.Utilities;
using Bit.Core.Enums;
using Bit.Core.Models.Interfaces;

namespace Bit.Core.Models.Table
{
    public class OrganizationUser : ITableObject<Guid>, IExternal, IPermissions
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid? UserId { get; set; }
        public string Email { get; set; }
        public string Key { get; set; }
        public OrganizationUserStatusType Status { get; set; }
        public OrganizationUserType Type { get; set; }
        public bool AccessAll { get; set; }
        public string ExternalId { get; set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;
        public bool AccessBusinessPortal { get; set; }
        public bool AccessEventLogs { get; set; }
        public bool AccessImportExport { get; set; }
        public bool AccessReports { get; set; }
        public bool ManageAllCollections { get; set; }
        public bool ManageAssignedCollections { get; set; }
        public bool ManageGroups { get; set; }
        public bool ManagePolicies { get; set; }
        public bool ManageUsers { get; set; }

        public void SetNewId()
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
