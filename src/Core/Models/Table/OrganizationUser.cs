using System;
using Bit.Core.Utilities;
using Bit.Core.Enums;

namespace Bit.Core.Models.Table
{
    public class OrganizationUser : ITableObject<Guid>, IExternal
    {
        [DbOrder(1)]
        public Guid Id { get; set; }

        [DbOrder(2)]
        public Guid OrganizationId { get; set; }

        [DbOrder(3)]
        public Guid? UserId { get; set; }

        [DbOrder(4)]
        public string Email { get; set; }

        [DbOrder(5)]
        public string Key { get; set; }

        [DbOrder(13)]
        public string ResetPasswordKey { get; set; }

        [DbOrder(6)]
        public OrganizationUserStatusType Status { get; set; }

        [DbOrder(7)]
        public OrganizationUserType Type { get; set; }

        [DbOrder(8)]
        public bool AccessAll { get; set; }

        [DbOrder(9)]
        public string ExternalId { get; set; }

        [DbOrder(10)]
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;

        [DbOrder(11)]
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;
        
        [DbOrder(12)]
        public string Permissions { get; set; }

        public void SetNewId()
        {
            Id = CoreHelpers.GenerateComb();
        }
    }
}
